/*
    Ingestions are taken one at a time.

    The first version of traffic.IngestWebRequests let several run at once and relied on UPDLOCK and HOLDLOCK to keep them from adding the
    same new value twice. When they added different new values to the same range of an index (a burst of new hosts, clients or user agents),
    they deadlocked (error 1205). They now take an application lock first, which makes the locking below uncontended.
*/

ALTER PROCEDURE traffic.IngestWebRequests
    @Requests traffic.WebRequestTableType READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    -- One ingestion at a time (of every instance of the service that writes to this database), the rest wait here. The batches are small
    -- and set-based, so waiting is cheap, and what is not cheap is the alternative: two ingestions that each add new hosts, clients or
    -- user agents take locks on the same ranges of the same indexes (the HOLDLOCK below) and deadlock, however carefully they are ordered.
    -- The lock goes with the transaction.
    DECLARE @lock int;
    EXEC @lock = sp_getapplock @Resource = 'traffic.IngestWebRequests', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 120000;
    IF @lock < 0
        THROW 50001, 'Another ingestion held the database for too long.', 1;

    -- The requests that are new: a message that is delivered twice (or a batch that is sent again after a failure) is ingested once.
    SELECT i.*
    INTO #Incoming
    FROM
    (
        SELECT r.*, ROW_NUMBER() OVER (PARTITION BY r.EventId ORDER BY r.ObservedUtc) AS RowNumber
        FROM @Requests r
        WHERE NOT EXISTS (SELECT 1 FROM traffic.WebRequest w WHERE w.EventId = r.EventId)
    ) i
    WHERE i.RowNumber = 1;

    -- The dimensions. A value that is not there yet is added; UPDLOCK and HOLDLOCK keep two ingestions of the same new value from both adding it.

    INSERT traffic.Source (Name, FirstSeenUtc, LastSeenUtc)
    SELECT i.Source, MIN(i.ObservedUtc), MAX(i.ObservedUtc)
    FROM #Incoming i
    WHERE NOT EXISTS (SELECT 1 FROM traffic.Source s WITH (UPDLOCK, HOLDLOCK) WHERE s.Name = i.Source)
    GROUP BY i.Source;

    UPDATE s SET LastSeenUtc = x.LastSeen
    FROM traffic.Source s
    JOIN (SELECT Source, MAX(ObservedUtc) AS LastSeen FROM #Incoming GROUP BY Source) x ON x.Source = s.Name
    WHERE s.LastSeenUtc < x.LastSeen;

    INSERT traffic.Proxy (SourceId, Name, ListenPort, IsTls, FirstSeenUtc, LastSeenUtc)
    SELECT s.SourceId, i.Proxy, i.ListenPort, i.IsTls, MIN(i.ObservedUtc), MAX(i.ObservedUtc)
    FROM #Incoming i
    JOIN traffic.Source s ON s.Name = i.Source
    WHERE NOT EXISTS (SELECT 1 FROM traffic.Proxy p WITH (UPDLOCK, HOLDLOCK)
                      WHERE p.SourceId = s.SourceId AND p.Name = i.Proxy AND p.ListenPort = i.ListenPort AND p.IsTls = i.IsTls)
    GROUP BY s.SourceId, i.Proxy, i.ListenPort, i.IsTls;

    UPDATE p SET LastSeenUtc = x.LastSeen
    FROM traffic.Proxy p
    JOIN traffic.Source s ON s.SourceId = p.SourceId
    JOIN (SELECT Source, Proxy, ListenPort, IsTls, MAX(ObservedUtc) AS LastSeen FROM #Incoming GROUP BY Source, Proxy, ListenPort, IsTls) x
        ON x.Source = s.Name AND x.Proxy = p.Name AND x.ListenPort = p.ListenPort AND x.IsTls = p.IsTls
    WHERE p.LastSeenUtc < x.LastSeen;

    INSERT traffic.Host (Name, FirstSeenUtc, LastSeenUtc)
    SELECT i.Host, MIN(i.ObservedUtc), MAX(i.ObservedUtc)
    FROM #Incoming i
    WHERE NOT EXISTS (SELECT 1 FROM traffic.Host h WITH (UPDLOCK, HOLDLOCK) WHERE h.Name = i.Host)
    GROUP BY i.Host;

    UPDATE h SET LastSeenUtc = x.LastSeen
    FROM traffic.Host h
    JOIN (SELECT Host, MAX(ObservedUtc) AS LastSeen FROM #Incoming GROUP BY Host) x ON x.Host = h.Name
    WHERE h.LastSeenUtc < x.LastSeen;

    INSERT traffic.ClientAddress (Address, FirstSeenUtc, LastSeenUtc)
    SELECT i.ClientAddress, MIN(i.ObservedUtc), MAX(i.ObservedUtc)
    FROM #Incoming i
    WHERE NOT EXISTS (SELECT 1 FROM traffic.ClientAddress c WITH (UPDLOCK, HOLDLOCK) WHERE c.Address = i.ClientAddress)
    GROUP BY i.ClientAddress;

    UPDATE c SET LastSeenUtc = x.LastSeen
    FROM traffic.ClientAddress c
    JOIN (SELECT ClientAddress, MAX(ObservedUtc) AS LastSeen FROM #Incoming GROUP BY ClientAddress) x ON x.ClientAddress = c.Address
    WHERE c.LastSeenUtc < x.LastSeen;

    INSERT traffic.HttpMethod (Name)
    SELECT DISTINCT i.Method
    FROM #Incoming i
    WHERE NOT EXISTS (SELECT 1 FROM traffic.HttpMethod m WITH (UPDLOCK, HOLDLOCK) WHERE m.Name = i.Method);

    INSERT traffic.Path (Hash, Text)
    SELECT n.PathHash, n.Path
    FROM (SELECT PathHash, Path, ROW_NUMBER() OVER (PARTITION BY PathHash ORDER BY ObservedUtc) AS RowNumber FROM #Incoming) n
    WHERE n.RowNumber = 1
      AND NOT EXISTS (SELECT 1 FROM traffic.Path p WITH (UPDLOCK, HOLDLOCK) WHERE p.Hash = n.PathHash);

    INSERT traffic.UserAgent (Hash, Text, Browser, OperatingSystem, DeviceType, IsBot, FirstSeenUtc)
    SELECT n.UserAgentHash, n.UserAgent, n.Browser, n.OperatingSystem, ISNULL(n.DeviceType, 'Unknown'), ISNULL(n.IsBot, 0), n.ObservedUtc
    FROM (SELECT UserAgentHash, UserAgent, Browser, OperatingSystem, DeviceType, IsBot, ObservedUtc,
                 ROW_NUMBER() OVER (PARTITION BY UserAgentHash ORDER BY ObservedUtc) AS RowNumber
          FROM #Incoming WHERE UserAgentHash IS NOT NULL) n
    WHERE n.RowNumber = 1
      AND NOT EXISTS (SELECT 1 FROM traffic.UserAgent u WITH (UPDLOCK, HOLDLOCK) WHERE u.Hash = n.UserAgentHash);

    INSERT traffic.Referrer (Hash, Text, HostName)
    SELECT n.ReferrerHash, n.Referrer, n.ReferrerHost
    FROM (SELECT ReferrerHash, Referrer, ReferrerHost, ROW_NUMBER() OVER (PARTITION BY ReferrerHash ORDER BY ObservedUtc) AS RowNumber
          FROM #Incoming WHERE ReferrerHash IS NOT NULL) n
    WHERE n.RowNumber = 1
      AND NOT EXISTS (SELECT 1 FROM traffic.Referrer r WITH (UPDLOCK, HOLDLOCK) WHERE r.Hash = n.ReferrerHash);

    INSERT traffic.ContentType (Name)
    SELECT DISTINCT i.ContentType
    FROM #Incoming i
    WHERE i.ContentType IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM traffic.ContentType c WITH (UPDLOCK, HOLDLOCK) WHERE c.Name = i.ContentType);

    -- The facts.
    INSERT traffic.WebRequest
        (EventId, ObservedUtc, ProxyId, HostId, ClientAddressId, MethodId, PathId, UserAgentId, ReferrerId, ContentTypeId,
         QueryString, HttpVersion, StatusCode, RequestBytes, ResponseBytes, DurationMs, ConnectionId)
    SELECT i.EventId, i.ObservedUtc, p.ProxyId, h.HostId, c.ClientAddressId, m.MethodId, pa.PathId, u.UserAgentId, r.ReferrerId, ct.ContentTypeId,
           i.Query, i.HttpVersion, i.StatusCode, i.RequestBytes, i.ResponseBytes, i.DurationMs, i.ConnectionId
    FROM #Incoming i
    JOIN traffic.Source s ON s.Name = i.Source
    JOIN traffic.Proxy p ON p.SourceId = s.SourceId AND p.Name = i.Proxy AND p.ListenPort = i.ListenPort AND p.IsTls = i.IsTls
    JOIN traffic.Host h ON h.Name = i.Host
    JOIN traffic.ClientAddress c ON c.Address = i.ClientAddress
    JOIN traffic.HttpMethod m ON m.Name = i.Method
    JOIN traffic.Path pa ON pa.Hash = i.PathHash
    LEFT JOIN traffic.UserAgent u ON u.Hash = i.UserAgentHash
    LEFT JOIN traffic.Referrer r ON r.Hash = i.ReferrerHash
    LEFT JOIN traffic.ContentType ct ON ct.Name = i.ContentType;

    SELECT @@ROWCOUNT AS Inserted;

    COMMIT TRANSACTION;
END;
GO
