using System.Collections.Concurrent;

namespace HttpClientTestServer.SessionState;

public record SessionStateFeature(ConcurrentDictionary<string, object> Items);