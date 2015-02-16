using System;

namespace ConsoleApplication22.Messages
{
    public class CategoryRemoved
    {
        public readonly Guid CategoryId;

        public CategoryRemoved(Guid categoryId)
        {
            CategoryId = categoryId;
        }
    }
}