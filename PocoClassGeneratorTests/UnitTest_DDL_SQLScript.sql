create database GeneratorDataBase;
use GeneratorDataBase;

CREATE TABLE [dbo].[table1](
	[ID] [int] IDENTITY(1,1) NOT NULL,
	[Name] [varchar](6) NOT NULL,
 CONSTRAINT [PK_table1_1] PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO;

CREATE TABLE [dbo].[table2](
	[ID] [int] NOT NULL,
	[AutoIncrementColumn] [int] IDENTITY(1,1) NOT NULL,
 CONSTRAINT [PK_table2] PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY];

-- دو قید تک‌ستونه مستقل (Code و Name) + یک قید مرکب (GroupId و TypeId):
-- Code و Name باید batch متفاوت بگیرند و GroupId/TypeId یک batch مشترک
CREATE TABLE [dbo].[table3](
	[ID] [int] IDENTITY(1,1) NOT NULL,
	[Code] [int] NOT NULL,
	[Name] [varchar](50) NOT NULL,
	[GroupId] [int] NOT NULL,
	[TypeId] [int] NOT NULL,
 CONSTRAINT [PK_table3] PRIMARY KEY CLUSTERED
(
	[ID] ASC
) ON [PRIMARY],
 CONSTRAINT [IX_table3_Code] UNIQUE NONCLUSTERED
(
	[Code] ASC
) ON [PRIMARY],
 CONSTRAINT [IX_table3_Name] UNIQUE NONCLUSTERED
(
	[Name] ASC
) ON [PRIMARY],
 CONSTRAINT [IX_table3_GroupType] UNIQUE NONCLUSTERED
(
	[GroupId] ASC,
	[TypeId] ASC
) ON [PRIMARY]
) ON [PRIMARY];

-- تک‌قید یکتا: باید بدون شماره تولید شود (سازگار با خروجی قبلی)
CREATE TABLE [dbo].[table4](
	[ID] [int] IDENTITY(1,1) NOT NULL,
	[Code] [int] NOT NULL,
	[Name] [varchar](50) NULL,
 CONSTRAINT [PK_table4] PRIMARY KEY CLUSTERED
(
	[ID] ASC
) ON [PRIMARY],
 CONSTRAINT [IX_table4_Code] UNIQUE NONCLUSTERED
(
	[Code] ASC
) ON [PRIMARY]
) ON [PRIMARY];