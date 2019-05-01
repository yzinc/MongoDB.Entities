﻿
using MongoDB.Entities;

namespace Benchmark.Models
{
    public class Category : Entity
    {
        public string Name { get; set; }
        public Many<BlogPost> Posts { get; set; }

        public Category()
        {
            this.InitManyToMany(() => Posts, b => b.Categories, Side.Invese);
        }
    }
}
