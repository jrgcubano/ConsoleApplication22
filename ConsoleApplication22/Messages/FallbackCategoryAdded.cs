using System;

namespace ConsoleApplication22.Messages
{
    public class FallbackCategoryAdded
    {
        public readonly Guid CategoryId;
        public readonly string Name;

        public FallbackCategoryAdded(Guid categoryId, string name)
        {
            CategoryId = categoryId;
            Name = name;
        }
    }
}