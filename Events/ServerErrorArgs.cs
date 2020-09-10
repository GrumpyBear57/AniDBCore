using System;

namespace AniDBCore.Events {
    public class ServerErrorArgs : EventArgs {
        public string Response { get; set; }
        
        public ServerErrorArgs(string response) {
            Response = response;
        }
    }
}