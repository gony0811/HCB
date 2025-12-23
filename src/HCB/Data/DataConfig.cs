using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HCB.Data.Entity;
using HCB.Data.Entity.Type;

namespace HCB.Data
{
    public class RoleConfig : IEntityTypeConfiguration<Role>
    {
        public void Configure(EntityTypeBuilder<Role> b)
        {
            b.ToTable("Roles");
            b.HasKey(x => x.Id);

            b.Property(x => x.Id)
             .ValueGeneratedOnAdd()
             .HasAnnotation("Sqlite:Autoincrement", true);

            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
            b.HasIndex(x => x.Name).IsUnique();
            b.Property(x => x.Description).HasMaxLength(200);
            b.Property(x => x.Password).HasMaxLength(200).IsRequired();
            b.Property(x => x.IsActive).HasDefaultValue(true);

            b.HasMany(x => x.ScreenAccesses)
             .WithOne(sa => sa.Role)
             .HasForeignKey(sa => sa.RoleId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(x => x.ManageTargets)
             .WithOne(m => m.Manager)
             .HasForeignKey(m => m.ManagerRoleId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(x => x.ManagedBy)
             .WithOne(m => m.Target)
             .HasForeignKey(m => m.TargetRoleId)
             .OnDelete(DeleteBehavior.Cascade);

        }
    }

    public class ScreenConfig : IEntityTypeConfiguration<Screen>
    {
        public void Configure(EntityTypeBuilder<Screen> b)
        {
            b.ToTable("Screens");
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).HasMaxLength(100).IsRequired();
            b.HasIndex(x => x.Code).IsUnique();
            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
            b.Property(x => x.Path).HasMaxLength(200);
            b.Property(x => x.DisplayOrder).HasDefaultValue(0);
            b.Property(x => x.IsEnabled).HasDefaultValue(true);

            b.HasMany(x => x.AccessBy)
             .WithOne(sa => sa.Screen)
             .HasForeignKey(sa => sa.ScreenId)
             .OnDelete(DeleteBehavior.Cascade);

        }
    }

    public class RoleScreenAccessConfig : IEntityTypeConfiguration<RoleScreenAccess>
    {
        public void Configure(EntityTypeBuilder<RoleScreenAccess> b)
        {
            b.ToTable("RoleScreenAccess");
            b.HasKey(x => new { x.RoleId, x.ScreenId });
            b.Property(x => x.Granted).HasDefaultValue(true);
            b.HasIndex(x => x.RoleId).HasName("IX_RoleScreenAccess_RoleId");
            b.HasIndex(x => x.ScreenId).HasName("IX_RoleScreenAccess_ScreenId");
        }
    }

    public class RoleManageRoleConfig : IEntityTypeConfiguration<RoleManageRole>
    {
        public void Configure(EntityTypeBuilder<RoleManageRole> b)
        {
            b.ToTable("RoleManageRole");
            b.HasKey(x => new { x.ManagerRoleId, x.TargetRoleId });
            b.Property(x => x.CanManage).HasDefaultValue(true);

            b.HasOne(x => x.Manager)
             .WithMany(r => r.ManageTargets)
             .HasForeignKey(x => x.ManagerRoleId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Target)
             .WithMany(r => r.ManagedBy)
             .HasForeignKey(x => x.TargetRoleId)
             .OnDelete(DeleteBehavior.Cascade);

        }
    }

    public class RecipeConfig : IEntityTypeConfiguration<Recipe>
    {
        public void Configure(EntityTypeBuilder<Recipe> b)
        {
            b.ToTable("Recipes");
            b.HasKey(x => x.Id);

            b.Property(x => x.Id)
             .ValueGeneratedOnAdd()
             .HasAnnotation("Sqlite:Autoincrement", true);

            b.Property(x => x.Name).IsRequired();
            b.HasIndex(x => x.Name).IsUnique();

            b.Property(x => x.IsActive).HasDefaultValue(false);

            b.Property(x => x.CreatedAt)
             .HasDefaultValueSql("strftime('%Y-%m-%dT%H:%M:%fZ','now')");

            b.HasIndex(x => x.IsActive)
             .HasFilter("IsActive = 1")
             .IsUnique()
             .HasName("UX_Recipes_OnlyOneActive");
        }
    }


    public class RecipeParamConfig : IEntityTypeConfiguration<RecipeParam>
    {
        public void Configure(EntityTypeBuilder<RecipeParam> e)
        {
            e.ToTable("RecipeParam");

            e.HasKey(x => x.Id);

            e.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            e.Property(x => x.Value)
                .IsRequired();

            e.Property(x => x.Minimum)
                .HasMaxLength(100)
                .IsRequired(false);

            e.Property(x => x.Maximum)
                .HasMaxLength(100)
                .IsRequired(false);

            e.Property(x => x.Description)
                .HasMaxLength(500)
                .IsRequired(false);

            e.Property(x => x.ValueType)
                .HasConversion<string>()   // Enum → string
                .IsRequired();

            e.Property(x => x.UnitType)
                .HasConversion<string>()   // Enum → string
                .IsRequired();

            e.Property(x => x.RecipeId)
                .IsRequired();

            e.HasOne(x => x.Recipe)
                .WithMany(r => r.ParamList)
                .HasForeignKey(x => x.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);   // Recipe 삭제 시 Param 삭제
        }
    }
    public class DeviceConfig : IEntityTypeConfiguration<Device>
    {
        public void Configure(EntityTypeBuilder<Device> builder)
        {
            builder.ToTable("Device");
            builder.HasKey(d => d.Id);

            builder.Property(d => d.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(d => d.FileName).HasMaxLength(100);
            builder.Property(d => d.InstanceName).HasMaxLength(100);
            builder.Property(d => d.Description).HasMaxLength(200);

            builder.Property(d => d.DeviceType)
                .HasConversion<string>()
                .HasMaxLength(50);

            // ✅ 1:1 관계 설정 (Composition)
            builder.HasOne(d => d.MotionDeviceDetail)
                   .WithOne(m => m.Device)
                   .HasForeignKey<MotionDeviceDetail>(m => m.DeviceId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(d => d.IoDeviceDetail)
                   .WithOne(m => m.Device)
                   .HasForeignKey<IoDeviceDetail>(m => m.DeviceId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class MotionDeviceDetailConfig : IEntityTypeConfiguration<MotionDeviceDetail>
    {
        public void Configure(EntityTypeBuilder<MotionDeviceDetail> e)
        {
            // PK
            e.HasKey(x => x.DeviceId);

            // 1:1 관계 - Device ↔ MotionDeviceDetail
            e.HasOne(x => x.Device)
                .WithOne(x => x.MotionDeviceDetail)
                .HasForeignKey<MotionDeviceDetail>(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
            // SET NULL or RESTRICT로 변경 가능

            // 속성 설정
            e.Property(x => x.Ip)
                .HasMaxLength(50)
                .IsRequired(false);

            e.Property(x => x.Port)
                .IsRequired();

            e.Property(x => x.MotionDeviceType)
                .HasConversion<string>()   // Enum → int 저장
                .IsRequired();

            // MotionList: 1 : N 관계
            e.HasMany(x => x.MotionList)
                .WithOne(x => x.ParentDeviceEntity)
                .HasForeignKey(x => x.ParentDeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class IoDeviceDetailConfig : IEntityTypeConfiguration<IoDeviceDetail>
    {
        public void Configure(EntityTypeBuilder<IoDeviceDetail> e)
        {
            // PK
            e.HasKey(x => x.DeviceId);

            // 1:1 관계 - Device ↔ MotionDeviceDetail
            e.HasOne(x => x.Device)
                .WithOne(x => x.IoDeviceDetail)
                .HasForeignKey<IoDeviceDetail>(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
            // SET NULL or RESTRICT로 변경 가능

            // 속성 설정
            e.Property(x => x.Ip)
                .HasMaxLength(50)
                .IsRequired(false);

            e.Property(x => x.Port)
                .IsRequired();

            e.Property(x => x.IoDeviceType)
                .HasConversion<string>()   // Enum → int 저장
                .IsRequired();

            // MotionList: 1 : N 관계
            e.HasMany(x => x.IoDataList)
                .WithOne(x => x.ParentDeviceEntity)
                .HasForeignKey(x => x.ParentDeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
    public class MotionConfig : IEntityTypeConfiguration<MotionEntity>
    {
        public void Configure(EntityTypeBuilder<MotionEntity> builder)
        {
            builder.ToTable("Motion");

            builder.HasKey(m => m.Id);

            builder.Property(m => m.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.Unit)
                .HasConversion<string>()
                .IsRequired();

            builder.HasMany(m => m.PositionList)
                .WithOne(p => p.Motion)
                .HasForeignKey(p => p.MotionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(m => m.ParameterList)
                .WithOne(p => p.Motion)
                .HasForeignKey(p => p.MotionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class IoDataConfig : IEntityTypeConfiguration<IoDataEntity>
    {
        public void Configure(EntityTypeBuilder<IoDataEntity> builder)
        {
            builder.ToTable("IoData");

            builder.HasKey(m => m.Id);

            builder.Property(m => m.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(m => m.IoDataType)
                .IsRequired()
                .HasConversion<string>();

            builder.Property(m => m.Address)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(m => m.Index)
                .IsRequired();

            builder.Property(x => x.Unit)
                .HasConversion<string>()
                .IsRequired();

            builder.Property(x => x.Unit)
                .HasConversion<string>()   // Enum → string
                .IsRequired();
        }
    }

    public class MotionPositionConfiguration : IEntityTypeConfiguration<MotionPosition>
    {
        public void Configure(EntityTypeBuilder<MotionPosition> builder)
        {
            builder.ToTable("MotionPosition");

            builder.HasKey(p => p.Id);

            builder.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(p => p.Speed)
                .HasDefaultValue(0.0);


            builder.Property(p => p.Position)
                .HasDefaultValue(0.0);


            // 관계: MotionPosition → Motion (N:1)
            builder.HasOne(p => p.Motion)
                .WithMany(m => m.PositionList)
                .HasForeignKey(p => p.MotionId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class MotionParameterConfig : IEntityTypeConfiguration<MotionParameter>
    {
        public void Configure(EntityTypeBuilder<MotionParameter> e)
        {
            e.HasKey(x => x.Id);

            e.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);


            e.Property(x => x.IntValue)
                .IsRequired(false);

            e.Property(x => x.DoubleValue)
                .IsRequired(false);

            e.Property(x => x.BoolValue)
                .IsRequired(false);

            e.Property(x => x.StringValue)
                .HasMaxLength(500)
                .IsRequired(false);


            e.Property(x => x.ValueType)
                .IsRequired()
                .HasConversion<string>();

            e.Property(x => x.UnitType)
                .IsRequired()
                .HasConversion<string>();

            e.HasOne(x => x.Motion)
                .WithMany(x => x.ParameterList)
                .HasForeignKey(x => x.MotionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public sealed class AlarmConfig : IEntityTypeConfiguration<Alarm>
    {
        public void Configure(EntityTypeBuilder<Alarm> e)
        {
            e.ToTable("Alarms");
            e.HasKey(x => x.Id);


            e.Property(x => x.Id)
                .ValueGeneratedOnAdd()
                .HasAnnotation("Sqlite:Autoincrement", true);

            e.Property(x => x.Code)
            .IsRequired();

            e.HasIndex(x => x.Code)
                .IsUnique();

            e.Property(x => x.Name).IsRequired().HasMaxLength(128);
            e.HasIndex(x => x.Name);

            e.Property(x => x.Description).HasMaxLength(512);
            e.Property(x => x.Action).HasMaxLength(512);

            // enum -> int 저장
            e.Property(x => x.Level)
                .HasConversion<int>()
                .HasColumnType("INTEGER")
                .HasDefaultValue(AlarmLevel.LIGHT);

            e.Property(x => x.Status)
                .HasConversion<int>()
                .HasColumnType("INTEGER")
                .HasDefaultValue(AlarmStatus.RESET);

            e.Property(x => x.Enable)
                .HasConversion<int>()
                .HasColumnType("INTEGER")
                .HasDefaultValue(AlarmEnable.ENABLED);

            // 날짜/시간 (SQLite TEXT/ISO8601)
            e.Property(x => x.LastRaisedAt)
                .HasColumnType("TEXT");

            e.HasIndex(x => x.LastRaisedAt);
        }
    }

    public sealed class AlarmHistoryConfig : IEntityTypeConfiguration<AlarmHistory>
    {
        public void Configure(EntityTypeBuilder<AlarmHistory> e)
        {
            e.ToTable("AlarmHistories");
            e.HasKey(x => x.Id);

            e.Property(x => x.AlarmId).IsRequired();

            // enum -> int 저장
            e.Property(x => x.Level)
                .HasConversion<int>()
                .HasColumnType("INTEGER");

            e.Property(x => x.Status)
                .HasConversion<int>()
                .HasColumnType("INTEGER");

            e.Property(x => x.UpdateTime)
                .HasColumnType("TEXT")
                .IsRequired();

            e.HasIndex(x => x.UpdateTime);
            e.HasIndex(x => new { x.AlarmId, x.UpdateTime });

            e.HasOne<Alarm>()
                .WithMany()
                .HasForeignKey(x => x.AlarmId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class LogConfig : IEntityTypeConfiguration<LogModel>
    {
        public void Configure(EntityTypeBuilder<LogModel> e)
        {
            e.ToTable("Logs"); // 테이블 이름 지정

            // Message는 필수
            e.Property(x => x.Message).IsRequired();

            // Level 최대 길이 지정
            e.Property(x => x.Level).HasMaxLength(16).IsRequired(false);

            // ★★★ 인덱스 추가 (시간 역순 검색에 유리) ★★★
            e.HasIndex(x => x.Timestamp).IsDescending();
        }
    }
}
