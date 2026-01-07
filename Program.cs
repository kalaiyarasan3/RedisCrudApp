using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

namespace RedisCrudApp
{
    class Program
    {
        private static IDatabase _redisDb;
        static async Task Main(string[] args)
        {
            try
            {
                var redis = ConnectionMultiplexer.Connect("localhost:6379");
                _redisDb = redis.GetDatabase();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"run redis.exe ");
                return;
            }
            

            var options=new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer("Server=KALAI;Database=TestDb;Trusted_Connection=true;TrustServerCertificate=true;")
                .Options;
            using var db=new AppDbContext(options);
            db.Database.EnsureCreated();

            Console.WriteLine("Redis CRUD");
            Console.WriteLine("----------");
            string CRUDOptions = "\n 1. Add \n 2. Get \n 3. Update \n 4. Delete \n 5.List \n 0. Exit \n";
            Console.WriteLine(CRUDOptions);

            string choice;
            while ((choice = Console.ReadLine()) != "0")
            {
                try
                {
                    switch (choice)
                    {
                        case "1":
                            await Create(db);
                            break;
                        case "2":
                            await Read(db);
                            break;
                        case "3":
                            await Update(db);
                            break;
                        case "4":
                            await Delete(db);
                            break;
                        case "5":
                            await List(db);
                            break;
                        default:
                            Console.WriteLine("Invalid choice");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }

                Console.WriteLine("\n" + CRUDOptions);

            }

            static async Task Create(AppDbContext db)
            {
                Console.Write("Name: ");
                string name = Console.ReadLine() ?? "";

                Console.Write("Price: ");
                decimal price = Convert.ToDecimal(Console.ReadLine());

                Console.Write("Stock: ");
                int stock = Convert.ToInt32(Console.ReadLine());
                var product = new Product { Name = name, Price = price, Stock = stock };
                db.Products.Add(product);
                await db.SaveChangesAsync();

                string key = $"product:{product.Id}";


                await _redisDb.StringSetAsync(key, JsonSerializer.Serialize(product));
                Console.WriteLine($"Added Product ID: {product.Id}");

            }

            static async Task Read(AppDbContext db)
            {
                Console.Write("Product ID: ");
                int id = Convert.ToInt32(Console.ReadLine());

                string key = $"product:{id}";

                var redisData = await _redisDb.StringGetAsync(key);
                if (redisData.HasValue)
                {
                    Console.WriteLine("From Redis Cache");
                    var p = JsonSerializer.Deserialize<Product>(redisData);
                    Console.WriteLine($"{p.Name} - {p.Price} - Stock: {p.Stock}");
                    return;
                }

                Console.WriteLine("Not in Redis - Getting from SQL Server...");
                var product = await db.Products.FindAsync(id);

                if (product != null)
                {
                    await _redisDb.StringSetAsync(key, JsonSerializer.Serialize(product));
                    Console.WriteLine("Got from DB and Cached!");
                    Console.WriteLine($"{product.Name} - {product.Price} - Stock: {product.Stock}");
                }
                else
                {
                    Console.WriteLine("Product not found");
                }
            }

            static async Task Update(AppDbContext db)
            {
                Console.Write("ID: ");
                int id = Convert.ToInt32(Console.ReadLine());

                var product = await db.Products.FindAsync(id);
                if (product == null)
                {
                    Console.WriteLine("Not found");
                    return;
                }

                Console.Write("New Price: ");
                product.Price = Convert.ToDecimal(Console.ReadLine());

                await db.SaveChangesAsync();

                await _redisDb.KeyDeleteAsync($"product:{id}");
                Console.WriteLine("Updated and Cache Cleared");
            }

            static async Task Delete(AppDbContext db)
            {
                Console.Write("ID: ");
                int id = Convert.ToInt32(Console.ReadLine());

                var product = await db.Products.FindAsync(id);
                if (product != null)
                {
                    db.Products.Remove(product);
                    await db.SaveChangesAsync();
                    await _redisDb.KeyDeleteAsync($"product:{id}");
                    Console.WriteLine("Deleted");
                }
                else
                {
                    Console.WriteLine("Not found");
                }
            }

            static async Task List(AppDbContext db)
            {
                var products = await db.Products.ToListAsync();
                Console.WriteLine($"\n {products.Count} Products:");
                foreach (var p in products)
                {
                    Console.WriteLine($"  {p.Id}: {p.Name} - {p.Price}");
                }
                Console.WriteLine();
            }

        }
    }
}
