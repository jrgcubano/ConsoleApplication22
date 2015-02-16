using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ConsoleApplication22.Messages;
using EventStore.ClientAPI;
using Newtonsoft.Json;
using Paramol.Executors;
using Paramol.SqlClient;
using Projac;

namespace ConsoleApplication22
{
    class Program
    {
        //Started a regular 3.0.1 eventstore instance using following commandline
        //  EventStore.ClusterNode.exe --mem-db=true --run-projections=All
        //Manually started $by_event_type projection after logging into the GUI
        //at http://localhost:2113

        static void Main(string[] args)
        {
            var settings = new ConnectionStringSettings(
                "Sql",
                "Data Source=(localdb)\\ProjectsV12;Initial Catalog=ProjacUsage;Integrated Security=SSPI;",
                "System.Data.SqlClient");
            var projector = new SqlProjector(
                Classification.Concat(Category),
                new TransactionalSqlCommandExecutor(
                    settings,
                    IsolationLevel.ReadCommitted));
            projector.Project(new object[] {new DropSchema(), new CreateSchema()});

            Task.Factory.StartNew(async () =>
            {
                var random = new Random();
                var client = EventStoreConnection.Create(
                    ConnectionSettings.Default,
                    new IPEndPoint(IPAddress.Loopback, 1113));
                await client.ConnectAsync();

                var categories = new List<Guid>();
                //Simulate some category administration
                for (var index = 0; index < 10; index++)
                {
                    var categoryId = Guid.NewGuid();
                    categories.Add(categoryId);
                    var fallbackCategoryId = Guid.NewGuid();
                    await client.AppendToStreamAsync(
                        "category-" + fallbackCategoryId.ToString("N"),
                        ExpectedVersion.Any,
                        new object[]
                        {
                            new FallbackCategoryAdded(fallbackCategoryId, "FallbackCategory" + index)
                        }.Select(@event => new EventData(
                            Guid.NewGuid(),
                            @event.GetType().FullName,
                            true,
                            Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(@event)),
                            new byte[0])));
                    await client.AppendToStreamAsync(
                        "category-" + categoryId.ToString("N"),
                        ExpectedVersion.Any,
                        new object[]
                        {
                            new CategoryAdded(categoryId, fallbackCategoryId, "Category" + index)
                        }.Select(@event => new EventData(
                            Guid.NewGuid(),
                            @event.GetType().FullName,
                            true,
                            Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(@event)),
                            new byte[0])));
                    if (index%2 == 0)
                    {
                        await client.AppendToStreamAsync(
                        "category-" + categoryId.ToString("N"),
                        ExpectedVersion.Any,
                        new object[]
                        {
                            new CategoryRemoved(categoryId)
                        }.Select(@event => new EventData(
                            Guid.NewGuid(),
                            @event.GetType().FullName,
                            true,
                            Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(@event)),
                            new byte[0])));
                    }
                }

                var statements = new List<Guid>();
                //Simulate some account creation + statements
                for (var index = 0; index < 10; index++)
                {
                    var accountId = Guid.NewGuid();
                    var statement1Id = Guid.NewGuid();
                    var statement2Id = Guid.NewGuid();
                    statements.Add(statement1Id);
                    statements.Add(statement2Id);
                    await client.AppendToStreamAsync(
                        "account-" + accountId.ToString("N"),
                        ExpectedVersion.Any,
                        new object[]
                        {
                            new AccountCreated(accountId, "Eddy Seanomore" + index),
                            new AccountStatementAdded(accountId, statement1Id, random.Next(1, 100)),
                            new AccountStatementAdded(accountId, statement2Id, random.Next(1, 100))
                        }.Select(@event => new EventData(
                            Guid.NewGuid(),
                            @event.GetType().FullName,
                            true,
                            Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(@event)),
                            new byte[0])));
                }

                //Simulate some categorization of statements
                //Bit artificial since we're assigning removed categories - but hey, example
                for (var index = 0; index < statements.Count; index++)
                {
                    var classificationId = Guid.NewGuid();
                    var statementId = statements[index];
                    var categoryId = categories[random.Next(0, categories.Count -1)];
                    await client.AppendToStreamAsync(
                        "classification-" + classificationId.ToString("N"),
                        ExpectedVersion.Any,
                        new object[]
                        {
                            new AccountStatementCategorized(classificationId, statementId, categoryId)
                        }.Select(@event => new EventData(
                            Guid.NewGuid(),
                            @event.GetType().FullName,
                            true,
                            Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(@event)),
                            new byte[0])));
                }

                //Do some projections (in a hackish way)
                var _ = client.SubscribeToStreamFrom("$et-ConsoleApplication22.Messages.AccountStatementCategorized", null, true, (subscription, @event) =>
                {
                    projector.
                        Project(
                            JsonConvert.DeserializeObject<AccountStatementCategorized>(
                                Encoding.UTF8.GetString(@event.Event.Data)));
                });
                await Task.Delay(TimeSpan.FromSeconds(5));
                _.Stop();

                var ___ = client.SubscribeToStreamFrom("$et-ConsoleApplication22.Messages.CategoryAdded", null, true, (subscription, @event) =>
                {
                    projector.
                        Project(
                            JsonConvert.DeserializeObject<CategoryAdded>(
                                Encoding.UTF8.GetString(@event.Event.Data)));
                });
                await Task.Delay(TimeSpan.FromSeconds(5));
                ___.Stop();

                //Figure out which statements need to fallback happens here
                var executor = new SqlCommandExecutor(settings);
                var __ = client.SubscribeToStreamFrom("$et-ConsoleApplication22.Messages.CategoryRemoved", null, true, (subscription, @event) =>
                {
                    var message = JsonConvert.DeserializeObject<CategoryRemoved>(
                        Encoding.UTF8.GetString(@event.Event.Data));
                    using (var reader = executor.
                        ExecuteReader(
                            TSql.QueryStatement(
@"SELECT A.[StatementId], C.[FallbackCategoryId] 
FROM [AccountStatementClassification] A INNER JOIN [Category] C ON A.[CategoryId] = C.[CategoryId]
WHERE C.[CategoryId] = @CategoryId",
                                new {CategoryId = TSql.UniqueIdentifier(message.CategoryId)})))
                    {
                        if (!reader.IsClosed)
                        {
                            while (reader.Read())
                            {
                                Console.WriteLine("Statement {0} needs to fallback to category {1}",
                                    reader.GetGuid(0),
                                    reader.GetGuid(1));
                            }
                        }
                    }
                });
                await Task.Delay(TimeSpan.FromSeconds(5));
                __.Stop();

            }, TaskCreationOptions.LongRunning);
            Console.ReadLine();
        }

        public static SqlProjection Classification = new SqlProjectionBuilder().
            When<AccountStatementCategorized>(message => TSql.NonQueryStatement(
                @"INSERT INTO [AccountStatementClassification] ([StatementId], [CategoryId]) VALUES (@StatementId, @CategoryId)",
                new
                {
                    StatementId = TSql.UniqueIdentifier(message.StatementId),
                    CategoryId = TSql.UniqueIdentifier(message.CategoryId)
                })).
            When<CreateSchema>(_ =>
                TSql.NonQueryStatement(
                    @"IF NOT EXISTS (SELECT * FROM SYSOBJECTS WHERE NAME='AccountStatementClassification' AND XTYPE='U')
  BEGIN
    CREATE TABLE [AccountStatementClassification] (
      [StatementId] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_AccountStatementClassification] PRIMARY KEY,
      [CategoryId] UNIQUEIDENTIFIER NOT NULL)
  END")).
            When<DropSchema>(_ =>
                TSql.NonQueryStatement(
                    @"IF EXISTS (SELECT * FROM SYSOBJECTS WHERE NAME='AccountStatementClassification' AND XTYPE='U')
  BEGIN
    DROP TABLE [AccountStatementClassification]
  END")).
            Build();

        public static SqlProjection Category = new SqlProjectionBuilder().
            When<CategoryAdded>(message => TSql.NonQueryStatement(
                @"INSERT INTO [Category] ([CategoryId], [FallbackCategoryId]) VALUES (@CategoryId, @FallbackCategoryId)",
                new
                {
                    CategoryId = TSql.UniqueIdentifier(message.CategoryId),
                    FallbackCategoryId = TSql.UniqueIdentifier(message.FallbackCategoryId)
                })).
            When<CreateSchema>(_ =>
                TSql.NonQueryStatement(
                    @"IF NOT EXISTS (SELECT * FROM SYSOBJECTS WHERE NAME='Category' AND XTYPE='U')
  BEGIN
    CREATE TABLE [Category] (
      [CategoryId] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_Category] PRIMARY KEY,
      [FallbackCategoryId] UNIQUEIDENTIFIER NOT NULL)
  END")).
            When<DropSchema>(_ =>
                TSql.NonQueryStatement(
                    @"IF EXISTS (SELECT * FROM SYSOBJECTS WHERE NAME='Category' AND XTYPE='U')
  BEGIN
    DROP TABLE [Category]
  END")).
            Build();
    }
}
