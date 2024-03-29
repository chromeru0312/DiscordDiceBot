﻿// <auto-generated />
using DiscordDiceBot.Discord;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace DiscordDiceBot.Migrations
{
    [DbContext(typeof(SettingContext))]
    [Migration("20231208144436_InitialCreate")]
    partial class InitialCreate
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.10")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("DiscordDiceBot.Discord.ChannelSetting", b =>
                {
                    b.Property<ulong>("GuildId")
                        .HasColumnType("bigint unsigned")
                        .HasColumnName("guild_id");

                    b.Property<ulong>("ChannelId")
                        .HasColumnType("bigint unsigned")
                        .HasColumnName("channel_id");

                    b.Property<string>("DefaultSystem")
                        .HasColumnType("varchar(255)")
                        .HasColumnName("default_system");

                    b.Property<bool>("IsEnabledMessageRoll")
                        .HasColumnType("tinyint(1)")
                        .HasColumnName("is_enabled_message_roll");

                    b.HasKey("GuildId", "ChannelId");

                    b.ToTable("channel_settings", (string)null);
                });

            modelBuilder.Entity("DiscordDiceBot.Discord.GuildSetting", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint unsigned")
                        .HasColumnName("id");

                    b.Property<string>("DefaultSystem")
                        .HasColumnType("varchar(255)")
                        .HasColumnName("default_system");

                    b.Property<bool>("IsEnabledMessageRoll")
                        .HasColumnType("tinyint(1)")
                        .HasColumnName("is_enabled_message_roll");

                    b.HasKey("Id");

                    b.ToTable("guild_settings", (string)null);
                });

            modelBuilder.Entity("DiscordDiceBot.Discord.ChannelSetting", b =>
                {
                    b.HasOne("DiscordDiceBot.Discord.GuildSetting", "Guild")
                        .WithMany("Channels")
                        .HasForeignKey("GuildId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Guild");
                });

            modelBuilder.Entity("DiscordDiceBot.Discord.GuildSetting", b =>
                {
                    b.Navigation("Channels");
                });
#pragma warning restore 612, 618
        }
    }
}
