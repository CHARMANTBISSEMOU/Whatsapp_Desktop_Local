using System.Collections.Generic;
using P2PChat.Core.Json;

namespace P2PChat.Core.Protocol
{
    /// <summary>Signal de contrôle d'un appel audio pair à pair.</summary>
    public sealed class CallSignal
    {
        public string CallId;
        public string Action;
        public string Sdp;
        public bool Video;

        public Dictionary<string, object> ToDict()
        {
            return new Dictionary<string, object>
            {
                { "id", CallId ?? "" }, { "a", Action ?? "" }, { "sdp", Sdp ?? "" }, { "video", Video }
            };
        }

        public static CallSignal FromDict(Dictionary<string, object> d)
        {
            if (d == null) return null;
            return new CallSignal
            {
                CallId = MiniJson.AsString(d, "id", ""),
                Action = MiniJson.AsString(d, "a", ""),
                Sdp = MiniJson.AsString(d, "sdp", ""),
                Video = MiniJson.AsString(d, "video", "false") == "true"
            };
        }
    }
}
