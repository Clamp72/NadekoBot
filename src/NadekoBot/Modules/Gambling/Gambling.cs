﻿using Discord;
using Discord.Commands;
using NadekoBot.Attributes;
using NadekoBot.Extensions;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NadekoBot.Services;
using Discord.WebSocket;
using NadekoBot.Services.Database;
using NadekoBot.Services.Database.Models;
using System.Collections.Generic;

//todo DB
namespace NadekoBot.Modules.Gambling
{
    [Module("$", AppendSpace = false)]
    public partial class Gambling : DiscordModule
    {
        public static string CurrencyName { get; set; }
        public static string CurrencyPluralName { get; set; }
        public static string CurrencySign { get; set; }
        
        public Gambling(ILocalization loc, CommandService cmds, DiscordSocketClient client) : base(loc, cmds, client)
        {
            using (var uow = DbHandler.UnitOfWork())
            {
                var conf = uow.BotConfig.GetOrCreate();

                CurrencyName = conf.CurrencyName;
                CurrencySign = conf.CurrencySign;
                CurrencyPluralName = conf.CurrencyPluralName;
            }
            
        }

        [LocalizedCommand, LocalizedDescription, LocalizedSummary]
        [RequireContext(ContextType.Guild)]
        public async Task Raffle(IUserMessage umsg, [Remainder] IRole role = null)
        {
            var channel = (ITextChannel)umsg.Channel;

            role = role ?? channel.Guild.EveryoneRole;

            var members = role.Members().Where(u => u.Status == UserStatus.Online);
            var membersArray = members as IUser[] ?? members.ToArray();
            var usr = membersArray[new Random().Next(0, membersArray.Length)];
            await channel.SendMessageAsync($"**Raffled user:** {usr.Username} (id: {usr.Id})").ConfigureAwait(false);
        }
        
        [LocalizedCommand("$$$"), LocalizedDescription("$$$"), LocalizedSummary("$$$")]
        [RequireContext(ContextType.Guild)]
        public async Task Cash(IUserMessage umsg, [Remainder] IUser user = null)
        {
            var channel = (ITextChannel)umsg.Channel;

            user = user ?? umsg.Author;
            long amount;
            BotConfig config;
            using (var uow = DbHandler.UnitOfWork())
            {
                amount = uow.Currency.GetUserCurrency(user.Id);
                config = uow.BotConfig.GetOrCreate();
            }

            await channel.SendMessageAsync($"{user.Username} has {amount} {config.CurrencySign}").ConfigureAwait(false);
        }
        
        [LocalizedCommand, LocalizedDescription, LocalizedSummary]
        [RequireContext(ContextType.Guild)]
        public async Task Give(IUserMessage umsg, long amount, [Remainder] IUser receiver)
        {
            var channel = (ITextChannel)umsg.Channel;
            if (amount <= 0)
                return;
            bool success = false;
            using (var uow = DbHandler.UnitOfWork())
            {
                success = uow.Currency.TryUpdateState(umsg.Author.Id, amount);
                if(success)
                    uow.Currency.TryUpdateState(umsg.Author.Id, amount);

                await uow.CompleteAsync();
            }
            if (!success)
            {
                await channel.SendMessageAsync($"{umsg.Author.Mention} You don't have enough {Gambling.CurrencyPluralName}s.").ConfigureAwait(false);
                return;
            }

            await channel.SendMessageAsync($"{umsg.Author.Mention} successfully sent {amount} {Gambling.CurrencyPluralName}s to {receiver.Mention}!").ConfigureAwait(false);
        }

        ////todo DB
        ////todo owner only
        //[LocalizedCommand, LocalizedDescription, LocalizedSummary]
        //[RequireContext(ContextType.Guild)]
        //public Task Award(IUserMessage umsg, long amount, [Remainder] IGuildUser usr) =>
        //    Award(umsg, amount, usr.Id);

        //[LocalizedCommand, LocalizedDescription, LocalizedSummary]
        //[RequireContext(ContextType.Guild)]
        //public async Task Award(IUserMessage umsg, long amount, [Remainder] ulong usrId)
        //{
        //    var channel = (ITextChannel)umsg.Channel;

        //    if (amount <= 0)
        //        return;

        //    await CurrencyHandler.AddFlowersAsync(usrId, $"Awarded by bot owner. ({umsg.Author.Username}/{umsg.Author.Id})", (int)amount).ConfigureAwait(false);

        //    await channel.SendMessageAsync($"{umsg.Author.Mention} successfully awarded {amount} {Gambling.CurrencyName}s to <@{usrId}>!").ConfigureAwait(false);
        //}

        ////todo owner only
        ////todo DB
        //[LocalizedCommand, LocalizedDescription, LocalizedSummary]
        //[RequireContext(ContextType.Guild)]
        //public Task Take(IUserMessage umsg, long amount, [Remainder] IGuildUser user) =>
        //    Take(umsg, amount, user.Id);

        //todo owner only
        //[LocalizedCommand, LocalizedDescription, LocalizedSummary]
        //[RequireContext(ContextType.Guild)]
        //public async Task Take(IUserMessage umsg, long amount, [Remainder] ulong usrId)
        //{
        //    var channel = (ITextChannel)umsg.Channel;
        //    if (amount <= 0)
        //        return;

        //    await CurrencyHandler.RemoveFlowers(usrId, $"Taken by bot owner.({umsg.Author.Username}/{umsg.Author.Id})", (int)amount).ConfigureAwait(false);

        //    await channel.SendMessageAsync($"{umsg.Author.Mention} successfully took {amount} {Gambling.CurrencyName}s from <@{usrId}>!").ConfigureAwait(false);
        //}

        [LocalizedCommand, LocalizedDescription, LocalizedSummary]
        [RequireContext(ContextType.Guild)]
        public async Task BetRoll(IUserMessage umsg, long amount)
        {
            var channel = (ITextChannel)umsg.Channel;

            if (amount < 1)
                return;

            var guildUser = (IGuildUser)umsg.Author;

            long userFlowers;
            using (var uow = DbHandler.UnitOfWork())
            {
                userFlowers = uow.Currency.GetOrCreate(umsg.Id).Amount;
            }

            if (userFlowers < amount)
            {
                await channel.SendMessageAsync($"{guildUser.Mention} You don't have enough {Gambling.CurrencyName}s. You only have {userFlowers}{Gambling.CurrencySign}.").ConfigureAwait(false);
                return;
            }

            await CurrencyHandler.RemoveCurrencyAsync(guildUser, "Betroll Gamble", amount, false).ConfigureAwait(false);

            var rng = new Random().Next(0, 101);
            var str = $"{guildUser.Mention} `You rolled {rng}.` ";
            if (rng < 67)
            {
                str += "Better luck next time.";
            }
            else if (rng < 90)
            {
                str += $"Congratulations! You won {amount * 2}{Gambling.CurrencySign} for rolling above 66";
                await CurrencyHandler.AddCurrencyAsync(guildUser, "Betroll Gamble", amount * 2, false).ConfigureAwait(false);
            }
            else if (rng < 100)
            {
                str += $"Congratulations! You won {amount * 3}{Gambling.CurrencySign} for rolling above 90.";
                await CurrencyHandler.AddCurrencyAsync(guildUser, "Betroll Gamble", amount * 3, false).ConfigureAwait(false);
            }
            else
            {
                str += $"👑 Congratulations! You won {amount * 10}{Gambling.CurrencySign} for rolling **100**. 👑";
                await CurrencyHandler.AddCurrencyAsync(guildUser, "Betroll Gamble", amount * 10, false).ConfigureAwait(false);
            }

            await channel.SendMessageAsync(str).ConfigureAwait(false);
        }

        //todo DB
        [LocalizedCommand, LocalizedDescription, LocalizedSummary]
        [RequireContext(ContextType.Guild)]
        public async Task Leaderboard(IUserMessage umsg)
        {
            var channel = (ITextChannel)umsg.Channel;

            IEnumerable<Currency> richest;
            using (var uow = DbHandler.UnitOfWork())
            {
                richest = uow.Currency.GetTopRichest(10);
            }
            if (!richest.Any())
                return;
            await channel.SendMessageAsync(
                richest.Aggregate(new StringBuilder(
$@"```xl
        ┏━━━━━━━━━━━━━━━━━━━━━┳━━━━━━━┓
        ┃        Id           ┃  $$$  ┃
        "),
                (cur, cs) => cur.AppendLine(
$@"┣━━━━━━━━━━━━━━━━━━━━━╋━━━━━━━┫
        ┃{(channel.Guild.GetUser(cs.UserId)?.Username.TrimTo(18, true) ?? cs.UserId.ToString()),-20} ┃ {cs,5} ┃")
                        ).ToString() + "┗━━━━━━━━━━━━━━━━━━━━━┻━━━━━━━┛```").ConfigureAwait(false);
        }
    }
}