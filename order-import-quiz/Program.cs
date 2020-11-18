using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;

namespace order_import_quiz
{
    public class Program
    {
        static void Main(string[] args) //args[0] == operation, args[1] == customers.txt, args[2] == orders.txt
        {
            if (args[0].ToLower() == "import")
            {
                Import(args);
            }
            else if (args[0].ToLower() == "clean")
            {
                Clean(args);
            }
            else if (args[0].ToLower() == "check")
            {
                Check(args);
            }
            else if (args[0].ToLower() == "full")
            {
                Clean(args);
                Import(args);
                Check(args);
            }
            else
            {
                Console.WriteLine("ERROR");
            }
        }

        public static async void Import(string[] args)
        {
            var factory = new OrderbookContextFactory();
            using var context = factory.CreateDbContext(args);

            var fileLinesCustomer = await File.ReadAllLinesAsync(args[1]);
            string[] line;

            for (int i = 1; i < fileLinesCustomer.Count(); i++)
            {
                line = fileLinesCustomer[i].Split("\t");
                var customer = new Customer { Name = line[0], CreditLimit = decimal.Parse(line[1]) };
                context.Customers.Add(customer);
            }
            await context.SaveChangesAsync();

            fileLinesCustomer = await File.ReadAllLinesAsync(args[2]);
            for (int i = 1; i < fileLinesCustomer.Count(); i++)
            {
                line = fileLinesCustomer[i].Split("\t");
                var customer = context.Customers.Where(c => c.Name == line[0]).ToArray();
                var order = new Order { CustomerId = customer[0].Id, OrderDate = DateTime.Parse(line[1]), OrderValue = decimal.Parse(line[2]) };
                context.Orders.Add(order);
            }
            await context.SaveChangesAsync();
        }

        public static async void Clean(string[] args)
        {
            var factory = new OrderbookContextFactory();
            using var context = factory.CreateDbContext(args);

            context.Customers.RemoveRange(context.Customers);
            context.Orders.RemoveRange(context.Orders);

            await context.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT('Orders', RESEED, 0)");
            await context.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT('Customers', RESEED, 0)");


            await context.SaveChangesAsync();
        }

        public static async void Check(string[] args)
        {
            var factory = new OrderbookContextFactory();
            using var context = factory.CreateDbContext(args);

            var orders = await context.Orders.ToListAsync();
            foreach (var customer in context.Customers)
            {
                var sumOfOrderValue = orders.Where(x => customer.Id == x.CustomerId).Sum(x => x.OrderValue);
                if (sumOfOrderValue > customer.CreditLimit)
                    Console.WriteLine($"Name: {customer.Name}:\tLimit: {customer.CreditLimit} OrderValue: {sumOfOrderValue}");
            }
        }
    }
}

//Create the model class
class Customer
{
    public int Id { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "decimal(8,2)")]
    public decimal CreditLimit { get; set; }
}

class Order
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public DateTime OrderDate { get; set; }

    [Column(TypeName = "decimal(8,2)")]
    public decimal OrderValue { get; set; }
}

class OrderbookContext : DbContext
{
    public DbSet<Order> Orders { get; set; }

    public DbSet<Customer> Customers { get; set; }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public OrderbookContext(DbContextOptions<OrderbookContext> options)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
            : base(options)
    {

    }
}

class OrderbookContextFactory : IDesignTimeDbContextFactory<OrderbookContext>
{
    public OrderbookContext CreateDbContext(string[]? args = null)
    {
        var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

        var optionsBuilder = new DbContextOptionsBuilder<OrderbookContext>();
        optionsBuilder
            // Uncomment the following line if you want to print generated
            // SQL statements on the console.
            .UseLoggerFactory(LoggerFactory.Create(builder => builder.AddConsole()))
            .UseSqlServer(configuration["ConnectionStrings:DefaultConnection"]);

        return new OrderbookContext(optionsBuilder.Options);
    }
}
