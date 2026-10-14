using IhiksTaxi.Domain;
using Microsoft.EntityFrameworkCore;

namespace IhiksTaxi.Infrastructure;

public class IhiksTaxiDbContext : DbContext
{
	public IhiksTaxiDbContext(DbContextOptions<IhiksTaxiDbContext> options) : base(options)
	{
	}

	public DbSet<Driver> Drivers => Set<Driver>();
	public DbSet<Vehicle> Vehicles => Set<Vehicle>();
	public DbSet<Passenger> Passengers => Set<Passenger>();
	public DbSet<Trip> Trips => Set<Trip>();
	public DbSet<TripStatus> TripStatuses => Set<TripStatus>();
	public DbSet<Tariff> Tariffs => Set<Tariff>();
	public DbSet<Rating> Ratings => Set<Rating>();

	// все имена таблиц и колонок — как в учебной БД IhiksTaxi (snake_case, из скрипта ЛР1)
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<Driver>(e =>
		{
			e.ToTable("driver");
			e.HasKey(x => x.Id).HasName("PK_driver");
			e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
			e.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(120).IsRequired();
			e.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(20).IsRequired();
			e.Property(x => x.ExperienceYears).HasColumnName("experience_years").IsRequired();
			e.Property(x => x.Category).HasColumnName("category").HasMaxLength(10).IsRequired();
			e.ToTable(t => t.HasCheckConstraint("CK_driver_experience", "experience_years >= 1 AND experience_years <= 60"));
		});

		modelBuilder.Entity<Vehicle>(e =>
		{
			e.ToTable("vehicle");
			e.HasKey(x => x.Id).HasName("PK_vehicle");
			e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
			e.Property(x => x.DriverId).HasColumnName("driver_id").IsRequired();
			e.Property(x => x.Brand).HasColumnName("brand").HasMaxLength(100).IsRequired();
			e.Property(x => x.Model).HasColumnName("model").HasMaxLength(100).IsRequired();
			e.Property(x => x.VehicleClass).HasColumnName("class").HasMaxLength(20).IsRequired();
			e.Property(x => x.Plate).HasColumnName("plate").HasMaxLength(15).IsRequired();
			e.Property(x => x.TariffId).HasColumnName("tariff_id").IsRequired();
			e.HasIndex(x => x.DriverId).HasDatabaseName("Vehicle_IX_DriverId");
			e.HasOne(x => x.Driver).WithMany(d => d.Vehicles)
				.HasForeignKey(x => x.DriverId).HasConstraintName("FK_vehicle_driver")
				.OnDelete(DeleteBehavior.NoAction);
			e.HasOne(x => x.Tariff).WithMany(t => t.Vehicles)
				.HasForeignKey(x => x.TariffId).HasConstraintName("FK_vehicle_tariff")
				.OnDelete(DeleteBehavior.Restrict);
		});

		modelBuilder.Entity<Passenger>(e =>
		{
			e.ToTable("passenger");
			e.HasKey(x => x.Id).HasName("PK_passenger");
			e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
			e.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(120).IsRequired();
			e.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(20).IsRequired();
			e.Property(x => x.Email).HasColumnName("email").HasMaxLength(120);
			e.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(256);
			e.HasIndex(x => x.Email).IsUnique().HasDatabaseName("Passenger_IX_Email");
		});

		modelBuilder.Entity<Trip>(e =>
		{
			e.ToTable("trip");
			e.HasKey(x => x.Id).HasName("PK_trip");
			e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
			e.Property(x => x.PassengerId).HasColumnName("passenger_id").IsRequired();
			e.Property(x => x.DriverId).HasColumnName("driver_id");
			e.Property(x => x.FromAddress).HasColumnName("from_address").HasMaxLength(200).IsRequired();
			e.Property(x => x.ToAddress).HasColumnName("to_address").HasMaxLength(200).IsRequired();
			e.Property(x => x.DistanceKm).HasColumnName("distance_km").HasColumnType("numeric(10,2)").IsRequired();
			e.Property(x => x.Cost).HasColumnName("cost").HasColumnType("numeric(18,2)").IsRequired();
			e.Property(x => x.CreatedAt).HasColumnName("trip_date").HasColumnType("timestamp without time zone").IsRequired();
			e.Property(x => x.DurationMin).HasColumnName("duration_min");
			e.Property(x => x.TripStatusId).HasColumnName("trip_status_id").IsRequired();
			e.ToTable(t =>
			{
				t.HasCheckConstraint("CK_trip_cost", "cost >= 0");
				t.HasCheckConstraint("CK_trip_distance", "distance_km >= 0");
			});
			e.HasIndex(x => x.PassengerId).HasDatabaseName("Trip_IX_PassengerId");
			e.HasIndex(x => x.DriverId).HasDatabaseName("Trip_IX_DriverId");
			e.HasOne(x => x.Passenger).WithMany(p => p.Trips)
				.HasForeignKey(x => x.PassengerId).HasConstraintName("FK_trip_passenger")
				.OnDelete(DeleteBehavior.NoAction);
			e.HasOne(x => x.Driver).WithMany(d => d.Trips)
				.HasForeignKey(x => x.DriverId).HasConstraintName("FK_trip_driver")
				.OnDelete(DeleteBehavior.NoAction);
			e.HasOne(x => x.TripStatus).WithMany(s => s.Trips)
				.HasForeignKey(x => x.TripStatusId).HasConstraintName("FK_trip_trip_status")
				.OnDelete(DeleteBehavior.Restrict);
		});

		modelBuilder.Entity<TripStatus>(e =>
		{
			e.ToTable("trip_status");
			e.HasKey(x => x.Id).HasName("PK_trip_status");
			e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
			e.Property(x => x.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
			e.HasIndex(x => x.Name).IsUnique().HasDatabaseName("TripStatus_IX_Name");
		});

		modelBuilder.Entity<Tariff>(e =>
		{
			e.ToTable("tariff");
			e.HasKey(x => x.Id).HasName("PK_tariff");
			e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
			e.Property(x => x.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
			e.Property(x => x.BaseCost).HasColumnName("base_cost").HasColumnType("numeric(10,2)").IsRequired();
			e.Property(x => x.RatePerKm).HasColumnName("rate_per_km").HasColumnType("numeric(10,2)").IsRequired();
			e.Property(x => x.RatePerMinute).HasColumnName("rate_per_minute").HasColumnType("numeric(10,2)").IsRequired();
			e.Property(x => x.DemandFactorDefault).HasColumnName("demand_factor_default").HasColumnType("numeric(4,2)").IsRequired();
			e.HasIndex(x => x.Name).IsUnique().HasDatabaseName("Tariff_IX_Name");
		});

		modelBuilder.Entity<Rating>(e =>
		{
			e.ToTable("rating");
			e.HasKey(x => x.Id).HasName("PK_rating");
			e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
			e.Property(x => x.DriverId).HasColumnName("driver_id").IsRequired();
			e.Property(x => x.TripId).HasColumnName("trip_id");
			e.Property(x => x.Rate).HasColumnName("rate").IsRequired();
			e.ToTable(t => t.HasCheckConstraint("CK_rating_rate", "rate >= 1 AND rate <= 5"));
			e.HasIndex(x => x.TripId).IsUnique().HasDatabaseName("Rating_IX_TripId");
			e.HasIndex(x => x.DriverId).HasDatabaseName("Rating_IX_DriverId");
			e.HasOne(x => x.Driver).WithMany(d => d.Ratings)
				.HasForeignKey(x => x.DriverId).HasConstraintName("FK_rating_driver")
				.OnDelete(DeleteBehavior.NoAction);
			e.HasOne(x => x.Trip).WithOne(t => t.Rating)
				.HasForeignKey<Rating>(x => x.TripId).HasConstraintName("FK_rating_trip")
				.OnDelete(DeleteBehavior.Cascade);
		});
	}
}
