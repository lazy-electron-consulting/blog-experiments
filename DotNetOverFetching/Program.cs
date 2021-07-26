using System;
using System.Data.SqlClient;
using System.Linq;
using AutoFixture;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace DotNetOverFetching
{
    class OverFetchingContext : DbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Table4>();
            modelBuilder.Entity<Table8>();
            modelBuilder.Entity<Table16>();
            modelBuilder.Entity<Table32>();
            modelBuilder.Entity<Table64>();
            modelBuilder.Entity<Table128>();
            modelBuilder.Entity<Table256>();
            modelBuilder.Entity<Table512>();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.UseSqlServer(Program.ConnectionString);
        }
    }

    [MarkdownExporterAttribute.GitHub]
    [RPlotExporter]
    public abstract class OverfetchingBenchmark
    {
        protected readonly Guid Id = Guid.NewGuid();

        [GlobalSetup]
        public void EnsureDatabase()
        {
            using var db = new OverFetchingContext();

            Seed<Table4>();
            Seed<Table16>();
            Seed<Table32>();
            Seed<Table64>();
            Seed<Table128>();
            Seed<Table256>();
            Seed<Table512>();

            db.SaveChanges();

            void Seed<T>() where T : TableBase
            {
                var f = new Fixture();
                db.Set<T>().Add(f.Build<T>().With(x => x.Id, Id).Create());
            }
        }

        protected abstract string Overfetch<T>() where T : TableBase;
        protected abstract string SelectOne<T>() where T : TableBase;

        [Params(4, 16, 32, 64, 128, 256, 512)]
        public int Width { get; set; }
        // TODO: narrow table with big data field

        [Benchmark]
        public string Overfetch() => Width switch
        {
            4 => Overfetch<Table4>(),
            8 => Overfetch<Table8>(),
            16 => Overfetch<Table16>(),
            32 => Overfetch<Table32>(),
            64 => Overfetch<Table64>(),
            128 => Overfetch<Table128>(),
            256 => Overfetch<Table256>(),
            512 => Overfetch<Table512>(),
            _ => throw new NotSupportedException()
        };


        [Benchmark]
        public string SelectOne() => Width switch
        {
            4 => SelectOne<Table4>(),
            8 => SelectOne<Table8>(),
            16 => SelectOne<Table16>(),
            32 => SelectOne<Table32>(),
            64 => SelectOne<Table64>(),
            128 => SelectOne<Table128>(),
            256 => SelectOne<Table256>(),
            512 => SelectOne<Table512>(),
            _ => throw new NotSupportedException()
        };
    }

    public class EfCoreOverfetch : OverfetchingBenchmark
    {

        protected override string Overfetch<T>()
        {
            using var db = new OverFetchingContext();
            return db.Set<T>().Single(x => x.Id == Id).Email;
        }

        protected override string SelectOne<T>()
        {
            using var db = new OverFetchingContext();
            return db.Set<T>()
                .Where(x => x.Id == Id)
                .Select(x => x.Email)
                .Single();
        }

        protected virtual IQueryable<T> Set<T>(DbContext db) where T : TableBase
            => db.Set<T>();

    }

    public class EfCoreOverfetchNoTracking : EfCoreOverfetch
    {
        protected override IQueryable<T> Set<T>(DbContext db)
            => db.Set<T>().AsNoTracking();
    }

    public class DapperOverfetch : OverfetchingBenchmark
    {

        protected override string Overfetch<T>()
        {
            using var db = new SqlConnection(Program.ConnectionString);
            return db.QuerySingle<T>(
                $"SELECT * FROM {typeof(T).Name} WHERE Id = @id", new { Id })
                .Email;
        }

        protected override string SelectOne<T>()
        {
            using var db = new SqlConnection(Program.ConnectionString);
            return db.QuerySingle<string>(
                $"SELECT Email FROM {typeof(T).Name} WHERE Id = @id", new { Id });
        }
    }

    class Program
    {
        internal const string ConnectionString = @"Server=localhost;Database=overfetch;MultipleActiveResultSets=true;User Id=sa;Password=9c56db0a-2f18-47b0-a3d5-8beefa93a9c7";

        static void Main(string[] args)
        {
            if ("seed".Equals(args.FirstOrDefault(), StringComparison.OrdinalIgnoreCase))
            {
                Seed();
                return;
            }

            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }


        private static void Seed()
        {

            using var db = new OverFetchingContext();
            db.Database.EnsureCreated();
            var f = new Fixture();

            Seed<Table4>();
            Seed<Table16>();
            Seed<Table32>();
            Seed<Table64>();
            Seed<Table128>();
            Seed<Table256>();
            Seed<Table512>();

            db.SaveChanges();

            void Seed<T>() where T : TableBase
            {
                db.Set<T>().AddRange(f.CreateMany<T>(1000));
            }
        }
    }
}
