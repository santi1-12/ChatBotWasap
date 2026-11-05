:ON ERROR EXIT
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
GO
USE [WhatsAppBot];
GO

IF OBJECT_ID(N'dbo.Administrator', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Administrator
    (
        AdminId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Administrator PRIMARY KEY,
        Email nvarchar(320) NOT NULL,
        NormalizedEmail AS UPPER(LTRIM(RTRIM(Email))) PERSISTED,
        PasswordHash nvarchar(512) NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_Administrator_IsActive DEFAULT (1),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Administrator_CreatedAt DEFAULT (SYSUTCDATETIME()),
        LastLoginAtUtc datetime2(3) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.WhatsAppIntegration', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WhatsAppIntegration
    (
        IntegrationId uniqueidentifier NOT NULL CONSTRAINT PK_WhatsAppIntegration PRIMARY KEY,
        WabaId nvarchar(64) NULL,
        PhoneNumberId nvarchar(64) NULL,
        BusinessPhoneNumber nvarchar(32) NULL,
        DisplayName nvarchar(256) NULL,
        ConnectionState nvarchar(32) NOT NULL,
        ConnectedAtUtc datetime2(3) NULL,
        LastConnectionErrorCode nvarchar(100) NULL,
        AccessTokenSecretRef nvarchar(512) NULL,
        MetaAppSecretRef nvarchar(512) NULL,
        WebhookVerifyTokenRef nvarchar(512) NULL,
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_WhatsAppIntegration_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_WhatsAppIntegration_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        IsActive bit NOT NULL CONSTRAINT DF_WhatsAppIntegration_IsActive DEFAULT (0),
        ActiveSlot AS (CASE WHEN IsActive = 1 THEN 1 ELSE NULL END) PERSISTED,
        CONSTRAINT CK_WhatsAppIntegration_ConnectionState
            CHECK (ConnectionState IN (N'NotConnected', N'Configuring', N'Connected', N'Error', N'Unknown'))
    );
END;
GO

IF OBJECT_ID(N'dbo.BotConfiguration', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BotConfiguration
    (
        IntegrationId uniqueidentifier NOT NULL CONSTRAINT PK_BotConfiguration PRIMARY KEY,
        IsBotEnabled bit NOT NULL CONSTRAINT DF_BotConfiguration_IsBotEnabled DEFAULT (0),
        ReplyMode nvarchar(32) NOT NULL CONSTRAINT DF_BotConfiguration_ReplyMode DEFAULT (N'FaqThenAi'),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_BotConfiguration_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_BotConfiguration_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_BotConfiguration_Integration FOREIGN KEY (IntegrationId)
            REFERENCES dbo.WhatsAppIntegration(IntegrationId),
        CONSTRAINT CK_BotConfiguration_ReplyMode CHECK (ReplyMode IN (N'FaqOnly', N'FaqThenAi', N'AiOnly'))
    );
END;
GO

IF OBJECT_ID(N'dbo.FrequentResponse', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FrequentResponse
    (
        FrequentResponseId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_FrequentResponse PRIMARY KEY,
        IntegrationId uniqueidentifier NOT NULL,
        QuestionOrIntent nvarchar(500) NOT NULL,
        AnswerText nvarchar(max) NOT NULL,
        Priority int NOT NULL,
        Category nvarchar(100) NULL,
        IsActive bit NOT NULL CONSTRAINT DF_FrequentResponse_IsActive DEFAULT (1),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FrequentResponse_CreatedAt DEFAULT (SYSUTCDATETIME()),
        ModifiedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FrequentResponse_ModifiedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_FrequentResponse_Integration FOREIGN KEY (IntegrationId)
            REFERENCES dbo.WhatsAppIntegration(IntegrationId),
        CONSTRAINT CK_FrequentResponse_Priority CHECK (Priority BETWEEN 0 AND 1000)
    );
END;
GO

IF OBJECT_ID(N'dbo.FrequentResponseExpression', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FrequentResponseExpression
    (
        ExpressionId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_FrequentResponseExpression PRIMARY KEY,
        FrequentResponseId bigint NOT NULL,
        ExpressionText nvarchar(500) NOT NULL,
        NormalizedExpression nvarchar(500) NOT NULL,
        CONSTRAINT FK_FrequentResponseExpression_Response FOREIGN KEY (FrequentResponseId)
            REFERENCES dbo.FrequentResponse(FrequentResponseId) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'dbo.Contact', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Contact
    (
        ContactId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Contact PRIMARY KEY,
        IntegrationId uniqueidentifier NOT NULL,
        WhatsAppUserId nvarchar(64) NOT NULL,
        DisplayName nvarchar(256) NULL,
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Contact_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Contact_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_Contact_Integration FOREIGN KEY (IntegrationId)
            REFERENCES dbo.WhatsAppIntegration(IntegrationId)
    );
END;
GO

IF OBJECT_ID(N'dbo.Conversation', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Conversation
    (
        ConversationId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Conversation PRIMARY KEY,
        IntegrationId uniqueidentifier NOT NULL,
        ContactId bigint NOT NULL,
        BasicStatus nvarchar(16) NOT NULL CONSTRAINT DF_Conversation_BasicStatus DEFAULT (N'Open'),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Conversation_CreatedAt DEFAULT (SYSUTCDATETIME()),
        LastMessageAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Conversation_LastMessageAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_Conversation_Integration FOREIGN KEY (IntegrationId)
            REFERENCES dbo.WhatsAppIntegration(IntegrationId),
        CONSTRAINT FK_Conversation_Contact FOREIGN KEY (ContactId) REFERENCES dbo.Contact(ContactId),
        CONSTRAINT CK_Conversation_BasicStatus CHECK (BasicStatus IN (N'Open', N'Closed'))
    );
END;
GO

IF OBJECT_ID(N'dbo.Message', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Message
    (
        MessageId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Message PRIMARY KEY,
        IntegrationId uniqueidentifier NOT NULL,
        ConversationId bigint NOT NULL,
        ProviderMessageId nvarchar(256) NULL,
        Direction nvarchar(16) NOT NULL,
        MessageType nvarchar(24) NOT NULL,
        ContentText nvarchar(max) NULL,
        ReplySource nvarchar(32) NULL,
        FrequentResponseId bigint NULL,
        ProcessingState nvarchar(24) NOT NULL CONSTRAINT DF_Message_ProcessingState DEFAULT (N'Received'),
        DeliveryState nvarchar(24) NULL,
        ProviderTimestampUtc datetime2(3) NULL,
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Message_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Message_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        FailureCode nvarchar(100) NULL,
        CONSTRAINT FK_Message_Integration FOREIGN KEY (IntegrationId)
            REFERENCES dbo.WhatsAppIntegration(IntegrationId),
        CONSTRAINT FK_Message_Conversation FOREIGN KEY (ConversationId)
            REFERENCES dbo.Conversation(ConversationId),
        CONSTRAINT FK_Message_FrequentResponse FOREIGN KEY (FrequentResponseId)
            REFERENCES dbo.FrequentResponse(FrequentResponseId),
        CONSTRAINT CK_Message_Direction CHECK (Direction IN (N'Inbound', N'Outbound')),
        CONSTRAINT CK_Message_Type CHECK (MessageType IN (N'Text', N'Audio', N'Image', N'Unsupported')),
        CONSTRAINT CK_Message_ProcessingState CHECK
            (ProcessingState IN (N'Received', N'Processing', N'Completed', N'Failed', N'NoReply')),
        CONSTRAINT CK_Message_DeliveryState CHECK
            (DeliveryState IS NULL OR DeliveryState IN (N'Pending', N'Sent', N'Delivered', N'Read', N'Failed'))
    );
END;
GO

IF OBJECT_ID(N'dbo.MediaAttachment', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MediaAttachment
    (
        MediaAttachmentId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MediaAttachment PRIMARY KEY,
        MessageId bigint NOT NULL,
        MetaMediaId nvarchar(128) NULL,
        MediaKind nvarchar(16) NOT NULL,
        MimeType nvarchar(128) NULL,
        SizeBytes bigint NULL,
        StorageReference nvarchar(512) NULL,
        AvailabilityState nvarchar(16) NOT NULL CONSTRAINT DF_MediaAttachment_State DEFAULT (N'Pending'),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MediaAttachment_CreatedAt DEFAULT (SYSUTCDATETIME()),
        ExpiresAtUtc datetime2(3) NULL,
        CONSTRAINT FK_MediaAttachment_Message FOREIGN KEY (MessageId) REFERENCES dbo.Message(MessageId),
        CONSTRAINT CK_MediaAttachment_Kind CHECK (MediaKind IN (N'Audio', N'Image')),
        CONSTRAINT CK_MediaAttachment_State CHECK
            (AvailabilityState IN (N'Pending', N'Available', N'Expired', N'Failed', N'Deleted'))
    );
END;
GO

IF OBJECT_ID(N'dbo.Transcript', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Transcript
    (
        MessageId bigint NOT NULL CONSTRAINT PK_Transcript PRIMARY KEY,
        TranscriptText nvarchar(max) NULL,
        State nvarchar(16) NOT NULL CONSTRAINT DF_Transcript_State DEFAULT (N'Pending'),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Transcript_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Transcript_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_Transcript_Message FOREIGN KEY (MessageId) REFERENCES dbo.Message(MessageId),
        CONSTRAINT CK_Transcript_State CHECK (State IN (N'Pending', N'Completed', N'Failed'))
    );
END;
GO

IF OBJECT_ID(N'dbo.WebhookInboxEvent', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WebhookInboxEvent
    (
        WebhookInboxEventId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_WebhookInboxEvent PRIMARY KEY,
        IntegrationId uniqueidentifier NULL,
        EventKey nvarchar(256) NOT NULL,
        EventType nvarchar(80) NOT NULL,
        NormalizedEventJson nvarchar(max) NOT NULL,
        State nvarchar(16) NOT NULL CONSTRAINT DF_WebhookInbox_State DEFAULT (N'Received'),
        AttemptCount int NOT NULL CONSTRAINT DF_WebhookInbox_AttemptCount DEFAULT (0),
        ReceivedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_WebhookInbox_ReceivedAt DEFAULT (SYSUTCDATETIME()),
        ProcessedAtUtc datetime2(3) NULL,
        LastFailureCode nvarchar(100) NULL,
        CONSTRAINT FK_WebhookInbox_Integration FOREIGN KEY (IntegrationId)
            REFERENCES dbo.WhatsAppIntegration(IntegrationId),
        CONSTRAINT CK_WebhookInbox_State CHECK (State IN (N'Received', N'Processing', N'Completed', N'Failed')),
        CONSTRAINT CK_WebhookInbox_Json CHECK (ISJSON(NormalizedEventJson) = 1)
    );
END;
GO

IF OBJECT_ID(N'dbo.MessageOutbox', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MessageOutbox
    (
        MessageId bigint NOT NULL CONSTRAINT PK_MessageOutbox PRIMARY KEY,
        IntegrationId uniqueidentifier NOT NULL,
        SourceInboundMessageId bigint NULL,
        State nvarchar(16) NOT NULL CONSTRAINT DF_MessageOutbox_State DEFAULT (N'Pending'),
        AttemptCount int NOT NULL CONSTRAINT DF_MessageOutbox_AttemptCount DEFAULT (0),
        NextAttemptAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MessageOutbox_NextAttemptAt DEFAULT (SYSUTCDATETIME()),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MessageOutbox_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MessageOutbox_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_MessageOutbox_Message FOREIGN KEY (MessageId) REFERENCES dbo.Message(MessageId),
        CONSTRAINT FK_MessageOutbox_SourceInbound FOREIGN KEY (SourceInboundMessageId) REFERENCES dbo.Message(MessageId),
        CONSTRAINT FK_MessageOutbox_Integration FOREIGN KEY (IntegrationId)
            REFERENCES dbo.WhatsAppIntegration(IntegrationId),
        CONSTRAINT CK_MessageOutbox_State CHECK (State IN (N'Pending', N'Sending', N'Sent', N'Failed'))
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.WhatsAppIntegration)
BEGIN
    DECLARE @IntegrationId uniqueidentifier = NEWID();
    INSERT dbo.WhatsAppIntegration (IntegrationId, ConnectionState, IsActive)
    VALUES (@IntegrationId, N'NotConnected', 0);

    INSERT dbo.BotConfiguration (IntegrationId, IsBotEnabled, ReplyMode)
    VALUES (@IntegrationId, 0, N'FaqThenAi');
END;
GO
