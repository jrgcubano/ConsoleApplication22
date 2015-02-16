using System;

namespace ConsoleApplication22.Messages
{
    public class CategoryAdded
    {
        public readonly Guid CategoryId;
        public readonly Guid FallbackCategoryId;
        public readonly string Name;

        public CategoryAdded(Guid categoryId, Guid fallbackCategoryId, string name)
        {
            CategoryId = categoryId;
            FallbackCategoryId = fallbackCategoryId;
            Name = name;
        }
    }
}