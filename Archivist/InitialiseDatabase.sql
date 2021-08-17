-- Straight script generation from SQL

/****** Object:  Table [dbo].[Log]    Script Date: 04/08/2021 12:28:53 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Log]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[Log](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[CreatedUTC] [datetime2](3) NOT NULL,
	[LogText] [varchar](8000) NOT NULL,
	[LogSeverity] [tinyint] NOT NULL,
	[FunctionName] [varchar](100) NULL,
 CONSTRAINT [PK_Log] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_Log_CreatedUTC]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[Log] ADD  CONSTRAINT [DF_Log_CreatedUTC]  DEFAULT (getutcdate()) FOR [CreatedUTC]
END
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DF_Log_LogSeverity]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[Log] ADD  CONSTRAINT [DF_Log_LogSeverity]  DEFAULT ((1)) FOR [LogSeverity]
END
GO
/****** Object:  StoredProcedure [dbo].[AddLogEntry]    Script Date: 04/08/2021 12:28:54 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AddLogEntry]') AND type in (N'P', N'PC'))
BEGIN
EXEC dbo.sp_executesql @statement = N'CREATE PROCEDURE [dbo].[AddLogEntry] AS' 
END
GO
ALTER PROCEDURE [dbo].[AddLogEntry]
	@FunctionName				varchar(100) = NULL,
	@LogText					varchar(8000),
	@LogSeverity				tinyint = 1
AS
BEGIN

	Insert Into [Log] (FunctionName, LogText, LogSeverity)
	Values (@FunctionName, @LogText, @LogSeverity)

END
GO
