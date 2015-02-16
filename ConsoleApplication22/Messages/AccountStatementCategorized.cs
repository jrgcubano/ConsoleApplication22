using System;

namespace ConsoleApplication22.Messages
{
    public class AccountStatementCategorized
    {
        public readonly Guid ClassificationId;
        public readonly Guid StatementId;
        public readonly Guid CategoryId;

        public AccountStatementCategorized(Guid classificationId, Guid statementId, Guid categoryId)
        {
            ClassificationId = classificationId;
            StatementId = statementId;
            CategoryId = categoryId;
        }
    }
}