using System;

namespace AniDBCore.Events {
    public class ClientErrorArgs : EventArgs {
        public string Message { get; set; }
        
        public ClientErrorArgs(string message) {
            Message = message;
        }
    }
}