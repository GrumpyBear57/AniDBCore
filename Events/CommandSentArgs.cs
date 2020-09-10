using System;
using AniDBCore.Commands;

namespace AniDBCore.Events {
    public class CommandSentArgs : EventArgs {
        public static Command Command { get; set; }

        public CommandSentArgs(Command command) {
            Command = command;
        }
    }
}