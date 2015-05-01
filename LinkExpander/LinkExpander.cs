﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using LitJson;
using NetIrc2;
using NetIrc2.Events;
using NetIrc2.Parsing;

namespace WendySharp
{
    class LinkExpander
    {
        private readonly Regex TwitterCompiledMatch;
        private readonly Regex YoutubeCompiledMatch;
        private readonly FixedSizedQueue<string> LastVideos;
        private readonly FixedSizedQueue<ulong> LastTweets;
        private readonly LinkExpanderConfig Config;

        public LinkExpander(IrcClient client)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "services.json");

            if (!File.Exists(path))
            {
                Log.WriteWarn("Twitter", "File config/services.json doesn't exist");

                return;
            }

            var data = File.ReadAllText(path);

            try
            {
                Config = JsonMapper.ToObject<LinkExpanderConfig>(data);

                if (Config.Twitter.AccessSecret == null ||
                    Config.Twitter.AccessToken == null ||
                    Config.Twitter.ConsumerKey == null ||
                    Config.Twitter.ConsumerSecret == null)
                {
                    throw new JsonException("Twitter keys cannot be null");
                }

                if (Config.Channels == null)
                {
                    Config.Channels = new List<string>();
                }
                else
                {
                    foreach (var channel in Config.Channels)
                    {
                        if (!IrcValidation.IsChannelName(channel))
                        {
                            throw new JsonException(string.Format("Invalid channel '{0}'", channel));
                        }
                    }
                }
            }
            catch (JsonException e)
            {
                Log.WriteError("Twitter", "Failed to parse services.json file: {0}", e.Message);

                Environment.Exit(1);
            }

            LastTweets = new FixedSizedQueue<ulong>();
            LastTweets.Limit = Config.DontRepeatLastCount;

            LastVideos = new FixedSizedQueue<string>();
            LastVideos.Limit = Config.DontRepeatLastCount;

            TwitterCompiledMatch = new Regex(@"(^|/|\.)twitter\.com/(.+?)/status/(?<status>[0-9]+)", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);
            YoutubeCompiledMatch = new Regex(@"(^|/|\.)(youtube\.com/watch\?v=|youtube\.com/embed/|youtu\.be/)(?<id>[a-zA-Z0-9\-_]+)", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);

            client.GotMessage += OnMessage;
        }

        private void OnMessage(object sender, ChatMessageEventArgs e)
        {
            if (!Config.Channels.Contains(e.Recipient))
            {
                return;
            }

            ProcessTwitter(e);
            ProcessYoutube(e);
        }

        private void ProcessTwitter(ChatMessageEventArgs e)
        {
            var matches = TwitterCompiledMatch.Matches(e.Message);

            foreach (Match match in matches)
            {
                var status = ulong.Parse(match.Groups["status"].Value);

                if (LastTweets.Contains(status))
                {
                    continue;
                }

                LastTweets.Enqueue(status);

                using (var webClient = new WebClient())
                {
                    var url = string.Format("https://api.twitter.com/1.1/statuses/show/{0}.json", status);
                    var authHeader = TwitterAuthorization.GetHeader("GET", url, Config.Twitter);

                    webClient.DownloadDataCompleted += (object s, DownloadDataCompletedEventArgs twitter) =>
                    {
                        if (twitter.Error != null || twitter.Cancelled)
                        {
                            Log.WriteError("Twitter", "Exception: {0}", twitter.Error.Message);
                            return;
                        }

                        var response = Encoding.UTF8.GetString(twitter.Result);
                        var tweet = JsonMapper.ToObject(response);

                        var text = WebUtility.HtmlDecode(tweet["text"].ToString()).Replace('\n', ' ').Trim();

                        if (Config.Twitter.ExpandURLs)
                        {
                            foreach (JsonData entityUrl in tweet["entities"]["urls"])
                            {
                                text = text.Replace(WebUtility.HtmlDecode((string)entityUrl["url"]), WebUtility.HtmlDecode((string)entityUrl["expanded_url"]));
                            }
                        }

                        DateTime date;

                        if (!DateTime.TryParseExact(tweet["created_at"].ToString(), "ddd MMM dd HH:mm:ss zz00 yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out date))
                        {
                            date = DateTime.UtcNow;
                        }

                        Bootstrap.Client.Client.Message(e.Recipient,
                            string.Format("{0}{1}{2} {3}:{4} {5}",
                                Color.BLUE,
                                tweet["user"]["name"].ToString(),
                                Color.NORMAL,
                                date.ToRelativeString(),
                                Color.LIGHTGRAY,
                                text
                            )
                        );
                    };

                    webClient.Headers.Add(HttpRequestHeader.Authorization, string.Format("OAuth {0}", authHeader));
                    webClient.DownloadDataAsync(new Uri(url));
                }
            }
        }

        private void ProcessYoutube(ChatMessageEventArgs e)
        {
            var matches = YoutubeCompiledMatch.Matches(e.Message);

            foreach (Match match in matches)
            {
                var id = match.Groups["id"].Value;

                if (LastVideos.Contains(id))
                {
                    continue;
                }

                LastVideos.Enqueue(id);

                using (var webClient = new WebClient())
                {
                    webClient.DownloadDataCompleted += (object s, DownloadDataCompletedEventArgs youtube) =>
                    {
                        if (youtube.Error != null || youtube.Cancelled)
                        {
                            return;
                        }

                        var response = Encoding.UTF8.GetString(youtube.Result);
                        var data = JsonMapper.ToObject(response);

                        // If original message already contains video title, don't post it again
                        if (e.Message.ToString().Contains(data["title"].ToString()))
                        {
                            return;
                        }

                        Bootstrap.Client.Client.Message(e.Recipient,
                            string.Format("{0}{1}{2} by {3}{4}",
                                Color.LIGHTGRAY,
                                data["title"],
                                Color.NORMAL,
                                Color.BLUE,
                                data["author_name"]
                            )
                        );
                    };

                    webClient.DownloadDataAsync(new Uri(string.Format("https://www.youtube.com/oembed?format=json&url=youtu.be/{0}", id)));
                }
            }
        }
    }
}