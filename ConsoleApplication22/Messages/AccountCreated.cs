using System;

namespace ConsoleApplication22.Messages
{
    public class AccountCreated
    {
        public readonly Guid AccountId;
        public readonly string AccountOwner;

        public AccountCreated(Guid accountId, string accountOwner)
        {
            AccountId = accountId;
            AccountOwner = accountOwner;
        }
    }
}