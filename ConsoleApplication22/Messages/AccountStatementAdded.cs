using System;

namespace ConsoleApplication22.Messages
{
    public class AccountStatementAdded
    {
        public readonly Guid AccountId;
        public readonly Guid StatementId;
        public readonly double Euros;

        public AccountStatementAdded(Guid accountId, Guid statementId, double euros)
        {
            AccountId = accountId;
            StatementId = statementId;
            Euros = euros;
        }
    }
}