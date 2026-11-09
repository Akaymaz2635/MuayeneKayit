using MuayeneKayit.Core;
using MuayeneKayit.Presentation;

// Admin'de tanımlanan ek ölçüm alanları (ör. Yükseklik): Core, muayene ekranı ve Admin testleri.
public static class CustomFieldTests
{
    public static readonly CustomFieldDef Height = new() { Key = "fheight1", Name = "Yükseklik", Abbrev = "H", Kind = CustomFieldKind.Length };
    public static readonly CustomFieldDef Offset = new() { Key = "foffset1", Name = "Sapma", Abbrev = "S", Kind = CustomFieldKind.Number };

    internal static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    public static void FieldDefinitionsSaveAndValidate()
    {
        var root = Path.Combine(Path.GetTempPath(), "MuayeneKayit-cf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Check(SharedConfig.LoadCustomFields(root).Count == 0, "Dosya yoksa boş liste");

            SharedConfig.SaveCustomFields(root, new[] { Height, Offset });
            var loaded = SharedConfig.LoadCustomFields(root);
            Check(loaded.Count == 2 && loaded[0].Name == "Yükseklik" && loaded[0].Abbrev == "H" && loaded[1].Kind == CustomFieldKind.Number,
                "Alanlar geri okunmalı");

            CustomFieldDef F(string name, string abbrev, string key = "fx") => new() { Key = key, Name = name, Abbrev = abbrev };
            Check(SharedConfig.ValidateCustomFields(new[] { F("Derinlik 2", "D") }).Count > 0, "Yerleşik kısaltma (D) reddedilmeli");
            Check(SharedConfig.ValidateCustomFields(new[] { F("<b>", "X") }).Count > 0, "Geçersiz ad reddedilmeli");
            Check(SharedConfig.ValidateCustomFields(new[] { F("Yükseklik", "") }).Count > 0, "Boş kısaltma reddedilmeli");
            Check(SharedConfig.ValidateCustomFields(new[] { F("A1", "H", "f1"), F("A2", "h", "f2") }).Count > 0, "Aynı kısaltma reddedilmeli");
            Check(SharedConfig.ValidateCustomFields(new[] { F("Aynı", "H1", "f1"), F("aynı", "H2", "f2") }).Count > 0, "Aynı ad reddedilmeli");

            // Şablon ek alan anahtarlarını saklar; eski şablon dosyasında alan yoksa boş liste.
            SharedConfig.SaveDefectTypes(root, new[] { new DefectTypeTemplate { Name = "Dent", Fields = { MeasureField.Depth }, CustomFields = { Height.Key } } });
            var types = SharedConfig.LoadDefectTypes(root);
            Check(types[0].CustomFields.SequenceEqual(new[] { Height.Key }), "Şablondaki ek alan korunmalı");
            Check(SharedConfig.FieldsOf(types[0], loaded).Single().Name == "Yükseklik", "Şablonun ek alan tanımları");
            var old = System.Text.Json.JsonSerializer.Deserialize<List<DefectTypeTemplate>>("[{\"name\":\"Dent\",\"fields\":[\"Depth\"]}]", SessionStore.JsonOptions)!;
            Check(old[0].CustomFields.Count == 0, "Eski şablonda ek alan yok");

            File.WriteAllText(Path.Combine(SharedConfig.Folder(root), "measure_fields.json"), "{bozuk");
            Check(SharedConfig.LoadCustomFields(root).Count == 0, "Bozuk dosyada boş liste, muayene durmaz");
        }
        finally { Directory.Delete(root, true); }
    }

    public static void SentenceReportCompareValidate()
    {
        var d = new Defect { Id = 1, DefectType = "Dent", Depth = 12, Custom = new() { CustomMeasures.Of(Height, 0.3, null) } };
        var sentence = DefectSentence.Statement(d);
        Check(sentence == "There is a Dent on the part: D 12 mm, H 0.3 mm.", $"Cümle: {sentence}");

        var inch = new Defect { DefectType = "Dent", Unit = LengthUnit.Inch, Custom = new() { CustomMeasures.Of(Height, 0.125, null), CustomMeasures.Of(Offset, -2, null) } };
        Check(DefectSentence.Statement(inch) == "There is a Dent on the part: H .125 in, S -2.", $"Inch cümle: {DefectSentence.Statement(inch)}");

        var session = new Session { PartNumber = "PD170", OperationNumber = "OP40", SerialNumber = "SN-CF", Inspector = "A" };
        var html = ReportBuilder.Build(session, new[] { d }, 1);
        Check(html.Contains($">{System.Net.WebUtility.HtmlEncode("Yükseklik")}<") && html.Contains(">0.3 mm<"), "Raporda ek ölçüm hücresi olmalı");

        // Rework öncesi / sonrası karşılaştırma ek ölçümü kendi adıyla yazar.
        var before = MeasurementEditor.Snapshot(d);
        var after = new Defect { DefectType = "Dent", Depth = 12, Custom = new() { CustomMeasures.Of(Height, 0.5, null) } };
        var lines = MeasurementCompare.Lines(before, after);
        Check(lines.Contains("Yükseklik: 0.3 → 0.5 mm (artış)"), $"Karşılaştırma: {string.Join(" | ", lines)}");
        var removed = MeasurementCompare.Lines(before, new Defect { DefectType = "Dent", Depth = 12 });
        Check(removed.Contains("Yükseklik: 0.3 mm → kaldırıldı"), $"Kaldırıldı: {string.Join(" | ", removed)}");

        // Doğrulama: uzunluk pozitif, sayı türünde eksi serbest.
        Check(DefectValidator.Validate(new Defect { DefectType = "Dent", Custom = new() { CustomMeasures.Of(Height, -1, null) } })
            .Any(e => e.Contains("Yükseklik")), "Negatif yükseklik reddedilmeli");
        Check(DefectValidator.Validate(new Defect { DefectType = "Dent", Custom = new() { CustomMeasures.Of(Offset, -1, null) } }).Count == 0,
            "Sayı türünde eksi kabul edilmeli");
    }

    public static void EditorEqualityAndOldRecords()
    {
        var d = new Defect { Id = 1, DefectType = "Dent", Depth = 2 };
        var snap = MeasurementEditor.Snapshot(d);
        Check(!MeasurementEditor.Apply(d, snap, "A"), "Değişiklik yoksa kayıt yok");

        var withHeight = snap with { Custom = new[] { CustomMeasures.Of(Height, 0.3, null) } };
        Check(MeasurementEditor.Apply(d, withHeight, "A"), "Ek ölçüm eklemek değişikliktir");
        Check(d.Custom!.Single().Number == 0.3 && d.MeasurementEdits.Count == 1, "Değer yazılmalı, tek düzeltme kaydı");
        Check(d.MeasurementEdits[0].Before.Custom is null && d.MeasurementEdits[0].After.Custom!.Count == 1, "Önce yok, sonra var");

        // Alan Admin'de yeniden adlandırılsa da aynı değer değişiklik sayılmaz.
        var renamed = new CustomFieldDef { Key = Height.Key, Name = "Yük.", Abbrev = "HH", Kind = CustomFieldKind.Length };
        Check(!MeasurementEditor.Apply(d, snap with { Custom = new[] { CustomMeasures.Of(renamed, 0.3, null) } }, "A"), "Ad farkı değişiklik sayılmamalı");

        // Boşaltmak değeri kaldırır.
        Check(MeasurementEditor.Apply(d, snap with { Custom = new[] { CustomMeasures.Of(Height, null, null) } }, "A") && d.Custom is null,
            "Boş değer kaldırılmalı");

        // JSON gidiş-dönüş ve ek alanı olmayan eski kayıt.
        d.Custom = new() { CustomMeasures.Of(Height, 0.4, null) };
        var json = System.Text.Json.JsonSerializer.Serialize(d, SessionStore.JsonOptions);
        var back = System.Text.Json.JsonSerializer.Deserialize<Defect>(json, SessionStore.JsonOptions)!;
        Check(back.Custom!.Single() == d.Custom[0], "Ek ölçüm JSON'da korunmalı");
        Check(back.MeasurementEdits.Last().After == d.MeasurementEdits.Last().After, "Düzeltme kaydı JSON'da korunmalı");
        var old = System.Text.Json.JsonSerializer.Deserialize<Defect>("{\"id\":1,\"defectType\":\"Dent\",\"depth\":1}", SessionStore.JsonOptions)!;
        Check(old.Custom is null && MeasurementEditor.Snapshot(old).Custom is null, "Eski kayıtta ek ölçüm yok");
    }
}

public static partial class ViewModelTests
{
    private static readonly IReadOnlyList<DefectTypeTemplate> DentWithHeight = new[]
    {
        new DefectTypeTemplate { Name = "Dent", Fields = { MeasureField.Depth }, CustomFields = { CustomFieldTests.Height.Key, CustomFieldTests.Offset.Key } },
        new DefectTypeTemplate { Name = "Scratch", Fields = { MeasureField.Length } },
    };

    private static readonly IReadOnlyList<CustomFieldDef> HeightFields = new[] { CustomFieldTests.Height, CustomFieldTests.Offset };

    private static void WithHeight(Action<Ctx> body) => With(body, DentWithHeight, HeightFields);

    public static void VmCustomFieldShownSavedAndInSentence() => WithHeight(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        Check(c.Vm.CustomInputs.Select(i => i.Header).SequenceEqual(new[] { "Yükseklik (mm)", "Sapma" }),
            $"Kutular: {string.Join(", ", c.Vm.CustomInputs.Select(i => i.Header))}");
        Check(c.Vm.IsSkippingMeasurement, "Ölçüm girilmemişken atlama uyarısı");

        c.Vm.DepthText = "0.5";
        c.Vm.CustomInputs[0].Text = "0.3";
        c.Vm.SaveMeasurements();
        var d = Load(c, 1);
        Check(d.Custom?.Single() is { Number: 0.3, Abbrev: "H" }, "Değer kayda yazılmalı");
        Check(c.Vm.Sentence.Contains("D 0.5 mm, H 0.3 mm."), $"Cümle: {c.Vm.Sentence}");
        Check(c.Vm.CustomInputs[0].Text == "0.3", "Kutu yenilenince değer korunur");

        // Yalnızca ek ölçümü değiştirmek düzeltme geçmişine yazılır.
        c.Vm.CustomInputs[0].Text = "0.4";
        c.Vm.AutoSave();
        Check(Load(c, 1).Custom!.Single().Number == 0.4, "Otomatik kayıt ek ölçümü yazmalı");
        Check(c.Vm.History.Any(h => h.Contains("H 0.4")), $"Geçmiş: {string.Join(" | ", c.Vm.History)}");

        // Başka tipte ek alan kutusu yok; kapalı kusurda kutular salt okunur.
        c.Vm.AddDefect("Scratch");
        Check(c.Vm.CustomInputs.Count == 0, "Scratch şablonunda ek alan yok");
        c.Vm.Select(1);
        Check(c.Vm.CustomInputs.All(i => !i.IsReadOnly), "Açık kusurda düzenlenebilir");
        c.Dialogs.Decisions.Enqueue(new DecisionInput(null, "ACC-1", null, null));
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Accept)));
        Check(c.Vm.CustomInputs.All(i => i.IsReadOnly), "Kapalı kusurda salt okunur");
    });

    public static void VmCustomFieldValidation() => WithHeight(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        c.Vm.CustomInputs[0].Text = "-1";
        c.Vm.SaveMeasurements();
        Check(c.Notes.Last().Kind == StatusKind.Error && c.Notes.Last().Message.Contains("Yükseklik"), $"Hata: {c.Notes.Last().Message}");
        Check(Load(c, 1).Custom is null, "Geçersiz değer yazılmamalı");

        c.Vm.CustomInputs[0].Text = "abc";
        c.Vm.SaveMeasurements();
        Check(c.Notes.Last().Kind == StatusKind.Error, "Sayı olmayan değer reddedilmeli");

        c.Vm.CustomInputs[0].Text = "";
        c.Vm.CustomInputs[1].Text = "-2.5";
        c.Vm.SaveMeasurements();
        Check(Load(c, 1).Custom!.Single().Number == -2.5, "Sayı türünde eksi kabul edilmeli");
    });

    public static void VmCustomFieldOrphanValueStaysVisible() => With(c =>
    {
        // Ek alan tanımı yok (Admin'den silinmiş) ama kayıtta değer var: kutu kayıttaki adıyla görünür.
        c.Open();
        c.Vm.AddDefect("Dent");
        c.Store.UpdateDefect(c.Folder, 1, d => d.Custom = new() { CustomMeasures.Of(CustomFieldTests.Height, 0.7, null) });
        c.Vm.ChangeSession();
        c.Vm.OpenSession();
        c.Vm.Select(1);
        Check(c.Vm.CustomInputs.Count == 1 && c.Vm.CustomInputs[0].Header == "Yükseklik (mm)" && c.Vm.CustomInputs[0].Text == "0.7",
            "Silinmiş alandaki değer gizlenmemeli");
    }, null, null);

    public static void VmReinspectMergesCustomField() => WithHeight(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        c.Vm.DepthText = "0.5";
        c.Vm.CustomInputs[0].Text = "0.3";
        c.Vm.SaveMeasurements();
        SendToRework(c);

        var request = c.Dialogs.ReinspectRequests;
        c.Dialogs.Reinspects.Enqueue(new ReinspectInput(ReinspectOutcome.Same, new MeasurementSnapshot(0.6, null, null, null, null, null), null, null, null));
        Wait(c.Vm.ReinspectAsync());
        Check(request[0].CustomFields!.Select(f => f.Key).SequenceEqual(HeightFields.Select(f => f.Key)), "Pencere ek alanları sormalı");

        var d = Load(c, 1);
        Check(d.Depth == 0.6 && d.Custom!.Single().Number == 0.3, $"Boş bırakılan yükseklik korunmalı: {d.Custom?.FirstOrDefault()?.Number}");
        Check(d.Dispositions.First(x => x.Decision == Decision.Rework).Snapshot!.Custom!.Single().Number == 0.3, "Rework anı görüntüsü ek ölçümü içermeli");

        // İkinci tur: yükseklik girilir, değişim satırında görünür.
        SendToRework(c);
        c.Dialogs.Reinspects.Enqueue(new ReinspectInput(ReinspectOutcome.Same,
            new MeasurementSnapshot(null, null, null, null, null, null, false, new[] { CustomMeasures.Of(CustomFieldTests.Height, 0.2, null) }), null, null, null));
        Wait(c.Vm.ReinspectAsync());
        Check(Load(c, 1).Custom!.Single().Number == 0.2, "Girilen yükseklik yazılmalı");
        Check(c.Vm.DeltaText.Contains("Yükseklik: 0.3 → 0.2 mm (azalış)"), $"Değişim: {c.Vm.DeltaText}");
    });
}

public static partial class AdminViewModelTests
{
    public static void TypesCustomFieldsAddCheckSaveDelete() => With(c =>
    {
        c.SignIn();
        c.Types.Load();
        Check(c.Types.FieldItems.Count == 0 && !c.Types.HasCustomFields, "Başta ek alan yok");

        // Yerleşik kısaltma reddedilir.
        c.Types.FieldName = "Derinlik 2";
        c.Types.FieldAbbrev = "D";
        c.Types.AddField();
        Check(c.Types.FieldItems.Count == 0 && c.Notes[^1].Kind == StatusKind.Error, "D kısaltması reddedilmeli");

        c.Types.FieldName = "Yükseklik";
        c.Types.FieldAbbrev = "H";
        c.Types.FieldKindIndex = (int)CustomFieldKind.Length;
        c.Types.AddField();
        Check(c.Types.FieldItems.Count == 1 && c.Types.FieldItems[0].StartsWith("Yükseklik   (H"), $"Alan listesi: {string.Join(", ", c.Types.FieldItems)}");
        Check(c.Types.HasCustomFields && c.Types.CustomToggles.Count == 1 && !c.Types.CustomToggles[0].IsChecked, "Onay kutusu çıkar, işaretsiz");

        // Seçili tipe (Dent) işaretle, uygula, kaydet.
        c.Types.SelectedIndex = 0;
        c.Types.CustomToggles[0].IsChecked = true;
        c.Types.Apply();
        Check(c.Types.Items[0].Contains(", H)"), $"Liste metni: {c.Types.Items[0]}");
        c.Types.Save();
        Check(c.Notes[^1].Kind == StatusKind.Success, $"Kaydedilmeli: {c.Notes[^1].Message}");
        var fields = SharedConfig.LoadCustomFields(c.Root);
        var types = SharedConfig.LoadDefectTypes(c.Root);
        Check(fields.Single().Name == "Yükseklik" && types[0].CustomFields.Single() == fields[0].Key, "Alan ve şablon dosyaya yazılmalı");
        Check(c.AuditActions().Contains("config.measure_fields"), "Denetim kaydı");

        // Ad güncellenir, anahtar değişmez.
        c.Types.SelectedFieldIndex = 0;
        c.Types.FieldName = "Yükseklik (pozitif)";
        c.Types.ApplyField();
        c.Types.Save();
        var updated = SharedConfig.LoadCustomFields(c.Root).Single();
        Check(updated.Name == "Yükseklik (pozitif)" && updated.Key == fields[0].Key, "Anahtar korunmalı");

        // Silmek tiplerden de kaldırır.
        c.Types.SelectedFieldIndex = 0;
        c.Confirm.Answers.Enqueue(true);
        c.Types.DeleteFieldAsync().GetAwaiter().GetResult();
        Check(c.Confirm.Calls[^1].Message.Contains(SharedConfig.Defaults[0].Name), "Onayda kullanan tipler yazılmalı");
        c.Types.Save();
        Check(SharedConfig.LoadCustomFields(c.Root).Count == 0 && SharedConfig.LoadDefectTypes(c.Root)[0].CustomFields.Count == 0, "Alan ve işaretler silinmeli");
    });
}
