namespace AniDBCore.Utils {
    internal struct Session {
        public static string ApiKey { get; private set; }
        
        public bool EncryptionEnabled { get; private set; }
        public bool LoggedIn { get; private set; }
        
        public string EncryptionKey { get; private set; }
        public string SessionKey { get; private set; }

        public static void SetApiKey(string apiKey) {
            // TODO make sure this is valid API key format
            ApiKey = apiKey;
        }

        public void EnableEncryption(string encryptionKey) {
            EncryptionKey = encryptionKey;
            EncryptionEnabled = true;
        }

        public void StartSession(string sessionKey) {
            SessionKey = sessionKey;
            LoggedIn = true;
        }

        public void EndSession() {
            EncryptionKey = string.Empty;
            SessionKey = string.Empty;

            EncryptionEnabled = false;
            LoggedIn = false;
        }
    }
}