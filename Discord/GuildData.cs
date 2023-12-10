using DiscordDiceBot.Dice;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace DiscordDiceBot.Discord
{
    public class SettingContext : DbContext
    {
        public DbSet<GuildSetting> Guild { get; set; }
        public DbSet<ChannelSetting> Channel { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseMySql(BotStart.Configuration["database"]!, new MariaDbServerVersion(new Version(10, 6, 12)));

        public async Task<GuildSetting> GetOrCreateGuildAsync(ulong guild)
        {
            var setting = await Guild.FindAsync(guild);
            if (setting == null)
            {
                setting = new(guild);
                await Guild.AddAsync(setting);
            }
            return setting;
        }
        public async Task<RollSetting> GetSettingAsync(ulong guild, ulong channel)
        {
            RollSetting? setting = await Channel.FindAsync(guild, channel);
            return setting ?? await GetOrCreateGuildAsync(guild);
        }
    }

    public static class EFExtention
    {
        public static PropertyBuilder<T> SetString<T>(this PropertyBuilder<T> builder, string name, bool requierd = true)
        {
            return builder.HasColumnName(name).HasColumnType("varchar(255)").IsRequired(requierd);
        }
    }

    [EntityTypeConfiguration(typeof(RollSettingConfiguration))]
    public abstract class RollSetting(string? system = null, bool message_roll = false)
    {
        public string? DefaultSystem { get; protected private set; } = system;
        public bool IsEnabledMessageRoll { get; protected private set; } = message_roll;

        public bool SetDefaultSystem(string? system)
        {
            string? id = null;
            if (system != null)
            {
                if (BCDice.Instance.ExistSystemFromId(system)) id = system;
                else if (!BCDice.Instance.ExistSystemFromName(system, out id)) return false;
            }

            bool changed = id != DefaultSystem;
            DefaultSystem = id;
            return changed;
        }

        public bool SetMessageRoll(bool enabled)
        {
            if (DefaultSystem == null) return false;

            var changed = enabled != IsEnabledMessageRoll;
            IsEnabledMessageRoll = enabled;
            return changed;
        }

        public class RollSettingConfiguration : IEntityTypeConfiguration<RollSetting>
        {
            public void Configure(EntityTypeBuilder<RollSetting> builder)
            {
                builder.UseTpcMappingStrategy();
            }
        }
    }
 
    [EntityTypeConfiguration(typeof(GuildSettingConfiguration))]
    public class GuildSetting : RollSetting
    {
        public ulong Id { get; }
        public IReadOnlyList<ChannelSetting> Channels { get; private set; } = new List<ChannelSetting>();

        public GuildSetting(ulong id) : base()
        {
            Id = id;
        }
        private GuildSetting(ulong id, string? defaultSystem, bool isEnabledMessageRoll)
            : base(defaultSystem, isEnabledMessageRoll)
        {
            Id = id;
        }

        public bool SetDefaultSystem(ulong channel, string? system)
        {
            var list = Channels.ToList();
            var idx = list.FindIndex(c => c.ChannelId == channel);

            if (idx == -1)
            {
                if (DefaultSystem == null) return false;
                list.Add(new(this, channel, system, false));
            }
            else
            {
                if (system == null && !list[idx].IsEnabledMessageRoll) list.RemoveAt(idx);
                else if (!list[idx].SetDefaultSystem(system)) return false;
            }

            Channels = list;
            return true;
        }

        public bool SetMessageRoll(ulong channel, bool enabled)
        {
            var list = Channels.ToList();
            var idx = list.FindIndex(c => c.ChannelId == channel);

            if (idx == -1)
            {
                if (DefaultSystem == null) return false;
                list.Add(new(this, channel, null, enabled));
            }
            else
            {
                if (list[idx].DefaultSystem == null && !enabled) list.RemoveAt(idx);
                else if (!list[idx].SetMessageRoll(enabled)) return false;
            }

            Channels = list;
            return true;
        }

        public class GuildSettingConfiguration : IEntityTypeConfiguration<GuildSetting>
        {
            public void Configure(EntityTypeBuilder<GuildSetting> builder)
            {
                builder.ToTable("guild_settings");
                builder.HasKey(g => g.Id);
                builder.Property(g => g.Id).HasColumnName("id").IsRequired();
                builder.Property(g => g.DefaultSystem).SetString("default_system", false);
                builder.Property(g => g.IsEnabledMessageRoll).HasColumnName("is_enabled_message_roll").IsRequired();
            }
        }
    }

    [EntityTypeConfiguration(typeof(ChannelSettingConfiguration))]
    public class ChannelSetting : RollSetting
    {
        public ulong GuildId { get; }
        public ulong ChannelId { get; }
        public new string? DefaultSystem { get => base.DefaultSystem ?? Guild.DefaultSystem; }
        public GuildSetting Guild { get; }

        public ChannelSetting(GuildSetting guild, ulong channel_id, string? system = null, bool message_roll = false)
            : base(system, message_roll)
        {
            GuildId = guild.Id;
            ChannelId = channel_id;
            Guild = guild;
        }
        private ChannelSetting(ulong guildId, ulong channelId, string? defaultSystem, bool isEnabledMessageRoll)
            : base(defaultSystem, isEnabledMessageRoll)
        {
            GuildId = guildId;
            ChannelId = channelId;
            Guild = null!;
        }

        public class ChannelSettingConfiguration : IEntityTypeConfiguration<ChannelSetting>
        {
            public void Configure(EntityTypeBuilder<ChannelSetting> builder)
            {
                builder.ToTable("channel_settings");
                builder.HasKey(c => new { c.GuildId, c.ChannelId });
                builder.Property(r => r.GuildId).HasColumnName("guild_id").IsRequired();
                builder.Property(r => r.ChannelId).HasColumnName("channel_id").IsRequired();
                builder.Property(r => r.DefaultSystem).SetString("default_system", false);
                builder.Property(r => r.IsEnabledMessageRoll).HasColumnName("is_enabled_message_roll").IsRequired();

                builder.HasOne(c => c.Guild).WithMany(g => g.Channels).HasForeignKey(c => c.GuildId);
                builder.Navigation(c => c.Guild).AutoInclude();
            }
        }
    }
}