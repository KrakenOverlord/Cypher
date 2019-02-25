﻿using System.Collections.Generic;
using System.Security;
using TangramCypher.ApplicationLayer.Actor;
using TangramCypher.ApplicationLayer.Wallet;

namespace TangramCypher.ApplicationLayer.Coin
{
    public interface ICoinService
    {
        CoinDto Build();
        double? Change();
        (CoinDto, CoinDto) CoinSwap(SecureString password, CoinDto coin, RedemptionKeyDto redemptionKey);
        byte[] Commit(ulong amount, int version, string stamp, SecureString password);
        byte[] Commit(ulong amount);
        byte[] Commit(ulong amount, byte[] blind);
        CoinDto DeriveCoin(SecureString password, CoinDto coin);
        CoinDto DeriveCoin(CoinDto coin);
        string DeriveKey(int version, string stamp, SecureString password, int bytes = 32);
        byte[] DeriveKey(int bytes = 32);
        byte[] DeriveKey(double? value, int bytes = 32);
        byte[] Hash(CoinDto coin);
        string HotRelease(int version, string stamp, string memo, SecureString password);
        string HotRelease(string memo);
        double? Input();
        CoinService Input(double? value);
        IEnumerable<CoinDto> MakeMultipleCoins(IEnumerable<TransactionDto> transactions, SecureString password);
        CoinDto MakeSingleCoin(TransactionDto transaction, SecureString password);
        CoinDto MakeSingleCoin();
        double? Output();
        CoinService Output(double? value);
        string PartialRelease(int version, string stamp, string memo, SecureString password);
        SecureString Password();
        CoinService Password(SecureString password);
        byte[] Sign(ulong amount, int version, string stamp, SecureString password, byte[] msg);
        byte[] Sign(ulong amount, byte[] msg);
        byte[] SignWithBlinding(byte[] msg, byte[] blinding);
        (byte[], byte[]) Split(byte[] blinding);
        string Stamp();
        CoinService Stamp(string stamp);
        CoinDto SwapPartialOne(SecureString password, CoinDto coin, RedemptionKeyDto redemptionKey);
        int VerifyCoin(CoinDto terminal, CoinDto current);
        int Version();
        CoinService Version(int version);
    }
}