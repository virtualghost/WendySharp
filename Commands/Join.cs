﻿using System;
using System.Collections.Generic;
using NetIrc2.Parsing;

namespace WendySharp
{
    class Join : Command
    {
        public Join()
        {
            Match = new List<string>
            {
                "join"
            };
            Usage = "<channel>";
            ArgumentMatch = "(?<channel>#+[\\w-]+)$";
            HelpText = "Joins an IRC channel.";
            Permission = "irc.control";
        }

        public override void OnCommand(CommandArguments command)
        {
            var channel = command.Arguments.Groups["channel"].Value;

            if (!IrcValidation.IsChannelName(channel))
            {
                command.Reply("That doesn't look like a valid channel name.");

                return;
            }

            Log.WriteInfo("Join", "'{0}' made us join channel {1}", command.Event.Sender, channel);

            Bootstrap.Client.Client.Join(channel);

            command.Reply("See you in {0}!", channel);
        }
    }
}
