using System;

namespace MongoDB.Entities
{
    /// <summary>
    /// Attribute for a migration
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class MigrationAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MigrationAttribute"/> class.
        /// </summary>
        /// <param name="number">The migration number</param>
        /// <param name="description">The migration description</param>
        public MigrationAttribute(int number, string description = null)
        {
            Number = number;            
            Description = description;
        }

        /// <summary>
        /// Gets the migration number
        /// </summary>
        public int Number { get; }

        /// <summary>
        /// Gets the description
        /// </summary>
        public string Description { get; }
    }
}
