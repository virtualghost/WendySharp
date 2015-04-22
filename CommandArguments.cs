﻿using System;
using NetIrc2.Events;
using System.Text.RegularExpressions;

namespace WendySharp
{
    class CommandArguments
    {
        public ChatMessageEventArgs Event;
        public Match Arguments;

        public void Reply(bool notice, string message, params object[] args)
        {
            Reply(string.Format(message, args), notice);
        }

        public void Reply(string message, params object[] args)
        {
            Reply(string.Format(message, args), false);
        }

        private void Reply(string message, bool notice)
        {
            string recipient = Event.Recipient;
            var isChannelMessage = Bootstrap.IsRecipientChannel(recipient);

            if (isChannelMessage)
            {
                if (!notice)
                {
                    message = string.Format("{0}: {1}", Event.Sender.Nickname, message);
                }
                else
                {
                    recipient = Event.Sender.Nickname.ToString();
                }
            }
            else
            {
                notice = false;

                recipient = Event.Sender.Nickname.ToString();
            }

            Bootstrap.Client.SendReply(recipient, message, notice);
        }
    }
}