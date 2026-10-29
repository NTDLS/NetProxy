/*
    The normalized schema of the traffic tracker.

    Every distinct value that a request has (the host, the client address, the user agent, the path, ...) is stored once in a table of its own,
    and the request (traffic.WebRequest, the fact table) refers to them by id. A request is a few numbers, however long its strings are, and
    "which user agents come to this host" is a join, not a scan of text.
*/

CREATE TABLE traffic.Source
(
    SourceId     int           IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Source PRIMARY KEY,
    Name         nvarchar(128) NOT NULL,
    FirstSeenUtc datetime2(3)  NOT NULL,
    LastSeenUtc  datetime2(3)  NOT NULL
);
CREATE UNIQUE INDEX UX_Source_Name ON traffic.Source (Name);
GO

CREATE TABLE traffic.Proxy
(
    ProxyId      int           IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Proxy PRIMARY KEY,
    SourceId     int           NOT NULL CONSTRAINT FK_Proxy_Source REFERENCES traffic.Source (SourceId),
    Name         nvarchar(128) NOT NULL,
    ListenPort   int           NOT NULL,
    IsTls        bit           NOT NULL,
    FirstSeenUtc datetime2(3)  NOT NULL,
    LastSeenUtc  datetime2(3)  NOT NULL
);
CREATE UNIQUE INDEX UX_Proxy_Identity ON traffic.Proxy (SourceId, Name, ListenPort, IsTls);
GO

CREATE TABLE traffic.Host
(
    HostId       int           IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Host PRIMARY KEY,
    Name         nvarchar(253) NOT NULL,
    FirstSeenUtc datetime2(3)  NOT NULL,
    LastSeenUtc  datetime2(3)  NOT NULL
);
CREATE UNIQUE INDEX UX_Host_Name ON traffic.Host (Name);
GO

CREATE TABLE traffic.ClientAddress
(
    ClientAddressId int          IDENTITY(1, 1) NOT NULL CONSTRAINT PK_ClientAddress PRIMARY KEY,
    Address         varchar(45)  NOT NULL,
    FirstSeenUtc    datetime2(3) NOT NULL,
    LastSeenUtc     datetime2(3) NOT NULL
);
CREATE UNIQUE INDEX UX_ClientAddress_Address ON traffic.ClientAddress (Address);
GO

CREATE TABLE traffic.HttpMethod
(
    MethodId smallint    IDENTITY(1, 1) NOT NULL CONSTRAINT PK_HttpMethod PRIMARY KEY,
    Name     varchar(32) NOT NULL
);
CREATE UNIQUE INDEX UX_HttpMethod_Name ON traffic.HttpMethod (Name);

INSERT traffic.HttpMethod (Name) VALUES ('GET'), ('POST'), ('PUT'), ('DELETE'), ('HEAD'), ('OPTIONS'), ('PATCH');
GO

CREATE TABLE traffic.Path
(
    PathId int            IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Path PRIMARY KEY,
    Hash   binary(32)     NOT NULL, -- SHA-256 of the text: a path is too long to be indexed, a hash is not.
    Text   nvarchar(2048) NOT NULL
);
CREATE UNIQUE INDEX UX_Path_Hash ON traffic.Path (Hash);
GO

CREATE TABLE traffic.UserAgent
(
    UserAgentId     int            IDENTITY(1, 1) NOT NULL CONSTRAINT PK_UserAgent PRIMARY KEY,
    Hash            binary(32)     NOT NULL,
    Text            nvarchar(1024) NOT NULL,
    Browser         nvarchar(64)   NULL,     -- Chrome, Firefox, Safari, Edge, curl, ...
    OperatingSystem nvarchar(64)   NULL,     -- Windows, macOS, Linux, Android, iOS, ...
    DeviceType      varchar(16)    NOT NULL, -- Desktop, Mobile, Tablet, Bot, Tool or Unknown
    IsBot           bit            NOT NULL,
    FirstSeenUtc    datetime2(3)   NOT NULL
);
CREATE UNIQUE INDEX UX_UserAgent_Hash ON traffic.UserAgent (Hash);
CREATE INDEX IX_UserAgent_Browser ON traffic.UserAgent (Browser, OperatingSystem);
GO

CREATE TABLE traffic.Referrer
(
    ReferrerId int            IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Referrer PRIMARY KEY,
    Hash       binary(32)     NOT NULL,
    Text       nvarchar(2048) NOT NULL,
    HostName   nvarchar(253)  NULL
);
CREATE UNIQUE INDEX UX_Referrer_Hash ON traffic.Referrer (Hash);
CREATE INDEX IX_Referrer_HostName ON traffic.Referrer (HostName);
GO

CREATE TABLE traffic.ContentType
(
    ContentTypeId smallint     IDENTITY(1, 1) NOT NULL CONSTRAINT PK_ContentType PRIMARY KEY,
    Name          varchar(127) NOT NULL
);
CREATE UNIQUE INDEX UX_ContentType_Name ON traffic.ContentType (Name);
GO

CREATE TABLE traffic.WebRequest
(
    RequestId       bigint           IDENTITY(1, 1) NOT NULL CONSTRAINT PK_WebRequest PRIMARY KEY,
    EventId         uniqueidentifier NOT NULL, -- a message can be delivered twice: an event that is already here is ignored
    ObservedUtc     datetime2(3)     NOT NULL,
    ProxyId         int              NOT NULL CONSTRAINT FK_WebRequest_Proxy REFERENCES traffic.Proxy (ProxyId),
    HostId          int              NOT NULL CONSTRAINT FK_WebRequest_Host REFERENCES traffic.Host (HostId),
    ClientAddressId int              NOT NULL CONSTRAINT FK_WebRequest_ClientAddress REFERENCES traffic.ClientAddress (ClientAddressId),
    MethodId        smallint         NOT NULL CONSTRAINT FK_WebRequest_HttpMethod REFERENCES traffic.HttpMethod (MethodId),
    PathId          int              NOT NULL CONSTRAINT FK_WebRequest_Path REFERENCES traffic.Path (PathId),
    UserAgentId     int              NULL CONSTRAINT FK_WebRequest_UserAgent REFERENCES traffic.UserAgent (UserAgentId),
    ReferrerId      int              NULL CONSTRAINT FK_WebRequest_Referrer REFERENCES traffic.Referrer (ReferrerId),
    ContentTypeId   smallint         NULL CONSTRAINT FK_WebRequest_ContentType REFERENCES traffic.ContentType (ContentTypeId),
    QueryString     nvarchar(2048)   NULL,
    HttpVersion     tinyint          NOT NULL, -- 10 for HTTP/1.0, 11 for HTTP/1.1
    StatusCode      smallint         NULL,
    RequestBytes    bigint           NULL,
    ResponseBytes   bigint           NULL,
    DurationMs      int              NULL,
    ConnectionId    uniqueidentifier NOT NULL
);
CREATE UNIQUE INDEX UX_WebRequest_EventId ON traffic.WebRequest (EventId);
CREATE INDEX IX_WebRequest_ObservedUtc ON traffic.WebRequest (ObservedUtc);
CREATE INDEX IX_WebRequest_Host ON traffic.WebRequest (HostId, ObservedUtc);
CREATE INDEX IX_WebRequest_ClientAddress ON traffic.WebRequest (ClientAddressId, ObservedUtc);
CREATE INDEX IX_WebRequest_UserAgent ON traffic.WebRequest (UserAgentId) WHERE UserAgentId IS NOT NULL;
CREATE INDEX IX_WebRequest_Path ON traffic.WebRequest (PathId, ObservedUtc);
GO

-- What the ingestion service sends to traffic.IngestWebRequests: one row for each request, with the values as text. The service works out
-- the hashes and what a user agent is, so that the procedure only has to match and insert.
CREATE TYPE traffic.WebRequestTableType AS TABLE
(
    EventId         uniqueidentifier NOT NULL,
    ObservedUtc     datetime2(3)     NOT NULL,
    Source          nvarchar(128)    NOT NULL,
    Proxy           nvarchar(128)    NOT NULL,
    ListenPort      int              NOT NULL,
    IsTls           bit              NOT NULL,
    Host            nvarchar(253)    NOT NULL,
    Method          varchar(32)      NOT NULL,
    Path            nvarchar(2048)   NOT NULL,
    PathHash        binary(32)       NOT NULL,
    Query           nvarchar(2048)   NULL,
    HttpVersion     tinyint          NOT NULL,
    ClientAddress   varchar(45)      NOT NULL,
    UserAgent       nvarchar(1024)   NULL,
    UserAgentHash   binary(32)       NULL,
    Browser         nvarchar(64)     NULL,
    OperatingSystem nvarchar(64)     NULL,
    DeviceType      varchar(16)      NULL,
    IsBot           bit              NULL,
    Referrer        nvarchar(2048)   NULL,
    ReferrerHash    binary(32)       NULL,
    ReferrerHost    nvarchar(253)    NULL,
    ContentType     varchar(127)     NULL,
    StatusCode      smallint         NULL,
    RequestBytes    bigint           NULL,
    ResponseBytes   bigint           NULL,
    DurationMs      int              NULL,
    ConnectionId    uniqueidentifier NOT NULL
);
GO

CREATE PROCEDURE traffic.IngestWebRequests
    @Requests traffic.WebRequestTableType READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

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

-- Removes the requests that are older than a time, a limited number at a time so that the log does not grow and the table is not locked
-- for long. Returns how many were removed; call it again until it returns less than @BatchSize.
CREATE PROCEDURE traffic.PurgeWebRequests
    @OlderThanUtc datetime2(3),
    @BatchSize    int = 10000
AS
BEGIN
    SET NOCOUNT ON;

    DELETE TOP (@BatchSize) FROM traffic.WebRequest WHERE ObservedUtc < @OlderThanUtc;

    SELECT @@ROWCOUNT AS Removed;
END;
GO

-- The requests as they read: every id replaced by what it stands for.
CREATE VIEW traffic.vWebRequest
AS
SELECT w.RequestId, w.ObservedUtc, s.Name AS Source, p.Name AS Proxy, p.ListenPort, p.IsTls, h.Name AS Host, c.Address AS ClientAddress,
       m.Name AS Method, pa.Text AS Path, w.QueryString, w.HttpVersion, w.StatusCode, ct.Name AS ContentType,
       w.RequestBytes, w.ResponseBytes, w.DurationMs,
       u.Text AS UserAgent, u.Browser, u.OperatingSystem, u.DeviceType, u.IsBot,
       r.Text AS Referrer, r.HostName AS ReferrerHost, w.ConnectionId
FROM traffic.WebRequest w
JOIN traffic.Proxy p ON p.ProxyId = w.ProxyId
JOIN traffic.Source s ON s.SourceId = p.SourceId
JOIN traffic.Host h ON h.HostId = w.HostId
JOIN traffic.ClientAddress c ON c.ClientAddressId = w.ClientAddressId
JOIN traffic.HttpMethod m ON m.MethodId = w.MethodId
JOIN traffic.Path pa ON pa.PathId = w.PathId
LEFT JOIN traffic.UserAgent u ON u.UserAgentId = w.UserAgentId
LEFT JOIN traffic.Referrer r ON r.ReferrerId = w.ReferrerId
LEFT JOIN traffic.ContentType ct ON ct.ContentTypeId = w.ContentTypeId;
GO

-- Requests, distinct clients and bytes of each host for each day (UTC).
CREATE VIEW traffic.vHostDaily
AS
SELECT h.Name AS Host,
       CAST(w.ObservedUtc AS date) AS Day,
       COUNT_BIG(*) AS Requests,
       COUNT(DISTINCT w.ClientAddressId) AS Clients,
       SUM(CAST(ISNULL(w.ResponseBytes, 0) AS bigint)) AS ResponseBytes,
       SUM(CASE WHEN w.StatusCode >= 500 THEN 1 ELSE 0 END) AS ServerErrors,
       AVG(w.DurationMs) AS AverageDurationMs
FROM traffic.WebRequest w
JOIN traffic.Host h ON h.HostId = w.HostId
GROUP BY h.Name, CAST(w.ObservedUtc AS date);
GO
