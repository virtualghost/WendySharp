﻿using System;
using System.Collections.Generic;
using NetIrc2;

namespace WendySharp
{
    class Flex : Command
    {
        public Flex()
        {
            Match = new List<string>
            {
                "flex"
            };
            HelpText = "OPs you for a few seconds, to show off your powah!";
            Permission = "irc.op.flex";
        }

        public override void OnCommand(CommandArguments command)
        {
            var channel = Bootstrap.Client.ChannelList.GetChannel(command.Event.Recipient);

            if (channel.Users[command.Event.Sender.Nickname] == Channel.Operator)
            {
                command.Reply("Silly billy, you're already an operator.");

                return;
            }

            if (!channel.WeAreOpped)
            {
                if (channel.HasChanServ)
                {
                    Bootstrap.Client.Client.Message("ChanServ", string.Format("op {0}", channel.Name));
                }
                else
                {
                    command.Reply("I'm not opped, send help.");

                    return;
                }
            }

            Bootstrap.Client.Client.Mode(command.Event.Recipient, "+o", new IrcString[1] { command.Event.Sender.Nickname });

            Bootstrap.Client.ModeList.AddLateModeRequest(
                new LateModeRequest
                {
                    Channel = command.Event.Recipient,
                    Recipient = command.Event.Sender.Nickname,
                    Sender = command.Event.Sender.ToString(),
                    Mode = "-o",
                    Time = DateTime.UtcNow.AddSeconds(10),
                    Reason = "Used flex command"
                }
            );
        }
    }
}
