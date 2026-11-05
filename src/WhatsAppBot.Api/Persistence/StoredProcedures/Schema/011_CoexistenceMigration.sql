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

IF COL_LENGTH(N'dbo.WhatsAppIntegration', N'TransitionState') IS NULL
BEGIN
    ALTER TABLE dbo.WhatsAppIntegration ADD TransitionState nvarchar(40) NOT NULL
        CONSTRAINT DF_WhatsAppIntegration_TransitionState DEFAULT (N'None');
END;
IF COL_LENGTH(N'dbo.WhatsAppIntegration', N'TransitionStartedAtUtc') IS NULL
    ALTER TABLE dbo.WhatsAppIntegration ADD TransitionStartedAtUtc datetime2(3) NULL;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_WhatsAppIntegration_TransitionState'
      AND parent_object_id = OBJECT_ID(N'dbo.WhatsAppIntegration')
)
    ALTER TABLE dbo.WhatsAppIntegration ADD CONSTRAINT CK_WhatsAppIntegration_TransitionState
        CHECK (TransitionState IN
            (N'None', N'PreparingMigration', N'MigrationToCoexistence', N'AwaitingBusinessApp',
             N'AwaitingCoexistenceSignup', N'Reconciling', N'Completed', N'Failed', N'Unknown'));
GO

IF EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_MessageOutbox_State'
      AND parent_object_id = OBJECT_ID(N'dbo.MessageOutbox')
)
    ALTER TABLE dbo.MessageOutbox DROP CONSTRAINT CK_MessageOutbox_State;
GO
ALTER TABLE dbo.MessageOutbox ADD CONSTRAINT CK_MessageOutbox_State
    CHECK (State IN (N'Pending', N'Sending', N'Sent', N'Failed', N'HeldForMigration'));
GO

IF OBJECT_ID(N'dbo.WhatsAppIntegrationMigration', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WhatsAppIntegrationMigration
    (
        MigrationId uniqueidentifier NOT NULL CONSTRAINT PK_WhatsAppIntegrationMigration PRIMARY KEY,
        TenantId uniqueidentifier NOT NULL,
        IntegrationId uniqueidentifier NOT NULL,
        TransitionState nvarchar(40) NOT NULL,
        CompletionState nvarchar(16) NOT NULL
            CONSTRAINT DF_WhatsAppIntegrationMigration_CompletionState DEFAULT (N'Pending'),
        ExternalMutationOccurred bit NOT NULL
            CONSTRAINT DF_WhatsAppIntegrationMigration_ExternalMutationOccurred DEFAULT (0),
        PreviousWabaId nvarchar(64) NULL,
        PreviousPhoneNumberId nvarchar(64) NULL,
        PreviousAccessTokenSecretRef nvarchar(512) NULL,
        PreviousOnboardingType nvarchar(16) NULL,
        PreviousEffectiveEmbeddedSignupConfigId nvarchar(128) NULL,
        NewWabaId nvarchar(64) NULL,
        NewPhoneNumberId nvarchar(64) NULL,
        NewAccessTokenSecretRef nvarchar(512) NULL,
        ExpectedExternalOperation nvarchar(256) NOT NULL,
        ExternalOperationReferenceId nvarchar(256) NULL,
        StartedAtUtc datetime2(3) NOT NULL,
        PreparingAtUtc datetime2(3) NULL,
        MigrationToCoexistenceAtUtc datetime2(3) NULL,
        AwaitingBusinessAppAtUtc datetime2(3) NULL,
        AwaitingCoexistenceSignupAtUtc datetime2(3) NULL,
        ReconcilingAtUtc datetime2(3) NULL,
        CompletedAtUtc datetime2(3) NULL,
        FailedAtUtc datetime2(3) NULL,
        UnknownAtUtc datetime2(3) NULL,
        ErrorCode nvarchar(100) NULL,
        ApprovalReference nvarchar(256) NULL,
        OperationalNotes nvarchar(2048) NULL,
        PreviousBotEnabled bit NOT NULL,
        PreviousReplyMode nvarchar(32) NOT NULL,
        CreatedAtUtc datetime2(3) NOT NULL
            CONSTRAINT DF_WhatsAppIntegrationMigration_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc datetime2(3) NOT NULL
            CONSTRAINT DF_WhatsAppIntegrationMigration_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_WhatsAppIntegrationMigration_Tenant
            FOREIGN KEY (TenantId) REFERENCES dbo.Tenant(TenantId),
        CONSTRAINT FK_WhatsAppIntegrationMigration_Integration
            FOREIGN KEY (TenantId, IntegrationId)
            REFERENCES dbo.WhatsAppIntegration(TenantId, IntegrationId),
        CONSTRAINT CK_WhatsAppIntegrationMigration_TransitionState
            CHECK (TransitionState IN
                (N'None', N'PreparingMigration', N'MigrationToCoexistence', N'AwaitingBusinessApp',
                 N'AwaitingCoexistenceSignup', N'Reconciling', N'Completed', N'Failed', N'Unknown')),
        CONSTRAINT CK_WhatsAppIntegrationMigration_CompletionState
            CHECK (CompletionState IN (N'Pending', N'Succeeded', N'Canceled', N'Failed', N'Unknown')),
        CONSTRAINT CK_WhatsAppIntegrationMigration_PreviousReplyMode
            CHECK (PreviousReplyMode IN (N'FaqOnly', N'FaqThenAi', N'AiOnly'))
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.WhatsAppIntegrationMigration')
      AND name = N'IX_WhatsAppIntegrationMigration_Tenant_Integration'
)
    CREATE INDEX IX_WhatsAppIntegrationMigration_Tenant_Integration
        ON dbo.WhatsAppIntegrationMigration(TenantId, IntegrationId, CreatedAtUtc DESC);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.WhatsAppIntegrationMigration')
      AND name = N'IX_WhatsAppIntegrationMigration_Active'
)
    CREATE INDEX IX_WhatsAppIntegrationMigration_Active
        ON dbo.WhatsAppIntegrationMigration(TenantId, IntegrationId, CompletionState, UpdatedAtUtc DESC);
GO
