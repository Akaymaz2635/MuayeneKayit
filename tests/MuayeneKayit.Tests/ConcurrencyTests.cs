using MuayeneKayit.Core;

// İki muayenecinin aynı ortak klasörde aynı anda çalışması: ayrı SessionStore örnekleri, paralel yazma.
// Gerçek ağ paylaşımının yerine geçmez; kilit ve atomik yazma kurallarının yarış altında doğru çalıştığını gösterir.
public static class ConcurrencyTests
{
    private static void Expect(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    private static string TempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "MuayeneKayit-conc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static Session Sample() => new()
    {
        PartNumber = "PD170", OperationNumber = "OP40", SerialNumber = "SN-PAR", Inspector = "A", Project = "PD170",
    };

    /// <summary>İki muayeneci aynı seride aynı anda kusur ekler: numaralar tekil ve sıralı, dosya kaybı yok.</summary>
    public static void ParallelAddDefectsGetUniqueIds()
    {
        var root = TempRoot();
        try
        {
            var a = new SessionStore(root);
            var b = new SessionStore(root);
            var (folder, _) = a.OpenOrCreate(Sample());
            b.OpenOrCreate(Sample());

            const int each = 25;
            var ta = Task.Run(() => { for (var i = 0; i < each; i++) a.AddDefect(folder, "Dent"); });
            var tb = Task.Run(() => { for (var i = 0; i < each; i++) b.AddDefect(folder, "Nick"); });
            Task.WaitAll(ta, tb);

            var ids = a.LoadDefects(folder).Select(d => d.Id).OrderBy(i => i).ToList();
            Expect(ids.Count == 2 * each, $"{2 * each} kusur olmalı, {ids.Count} var");
            Expect(ids.SequenceEqual(Enumerable.Range(1, 2 * each)), "Numaralar 1..50 tekil ve boşluksuz olmalı");
        }
        finally { Directory.Delete(root, true); }
    }

    /// <summary>İki muayeneci aynı kusuru aynı anda günceller: hiçbir güncelleme kaybolmaz (kayıp güncelleme yok).</summary>
    public static void ParallelUpdatesSameDefectLoseNothing()
    {
        var root = TempRoot();
        try
        {
            var a = new SessionStore(root);
            var b = new SessionStore(root);
            var (folder, _) = a.OpenOrCreate(Sample());
            var id = a.AddDefect(folder, "Dent").Id;

            const int each = 20;
            var ta = Task.Run(() => { for (var i = 0; i < each; i++) a.UpdateDefect(folder, id, d => d.Photos.Add($"a{i}")); });
            var tb = Task.Run(() => { for (var i = 0; i < each; i++) b.UpdateDefect(folder, id, d => d.Photos.Add($"b{i}")); });
            Task.WaitAll(ta, tb);

            var photos = a.LoadDefects(folder).Single().Photos;
            Expect(photos.Count == 2 * each, $"{2 * each} güncelleme olmalı, {photos.Count} var");
            Expect(photos.Distinct().Count() == 2 * each, "Güncellemeler tekrarsız olmalı");
        }
        finally { Directory.Delete(root, true); }
    }

    /// <summary>İki muayeneci aynı anda denetim kaydına yazar: zincir kırılmaz, satır kaybolmaz.</summary>
    public static void ParallelAuditAppendsKeepChain()
    {
        var root = TempRoot();
        try
        {
            var a = new SessionStore(root);
            var b = new SessionStore(root);

            const int each = 30;
            var ta = Task.Run(() => { for (var i = 0; i < each; i++) AuditLog.Append(a, "A", "test", $"a{i}"); });
            var tb = Task.Run(() => { for (var i = 0; i < each; i++) AuditLog.Append(b, "B", "test", $"b{i}"); });
            Task.WaitAll(ta, tb);

            var (ok, bad) = AuditLog.Verify(a);
            Expect(ok, $"Denetim zinciri sağlam olmalı (bozuk satır {bad})");
            var lines = File.ReadAllLines(Path.Combine(root, AuditLog.FileName)).Count(l => l.Length > 0);
            Expect(lines == 2 * each, $"{2 * each} satır olmalı, {lines} var");
        }
        finally { Directory.Delete(root, true); }
    }
}
