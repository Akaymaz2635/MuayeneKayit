// Önceden ŞÜPHELİ listedeki (B-01..B-22, N-2) testler: hatalar düzeltildi, testler ana listede koşar.
public static class FonkFixedTests
{
    public static List<(string Name, Action Body)> All { get; } = new()
    {
        ("FONK F-MEAS-018 [B-01 düzeltildi]: kayıtlı ölçüm otomatik kayıtla yuvarlanmaz (mm, açı, inç, Rework Snapshot)", ViewModelTests.Fonk_MEAS018_RoundTripKeepsPrecision),
        ("FONK F-CFG-008 [B-02]: bozuk admin.json / bozuk Base64 login'i çökertmez", AdminViewModelTests.Fonk_CFG008_CorruptAdminJsonDoesNotCrashLogin),
        ("FONK F-CFG-009 [B-02]: bozuk session.json oturum açmayı çökertmez", ViewModelTests.Fonk_CFG009_CorruptSessionJsonDoesNotCrash),
        ("FONK F-CFG-010 [B-02]: bozuk defects\\N.json oturum/Dashboard/Index'i çökertmez", ViewModelTests.Fonk_CFG010_CorruptDefectFileDoesNotCrash),
        ("FONK F-CFG-011 [B-02]: _audit.log yarım son satır işlemi bozmaz", ViewModelTests.Fonk_CFG011_TruncatedAuditTailDoesNotBreakOperations),
        ("FONK F-CONC-004 [B-03 düzeltildi]: eski ekrandaki kullanıcı başkasının ölçümünü ezmez", ViewModelTests.Fonk_CONC004_StaleFormDoesNotOverwriteOtherUsersMeasurement),
        ("FONK F-COMP-004 [B-04 düzeltildi]: tamamlanmış oturuma açık kusur eklenemez", ViewModelTests.Fonk_COMP004_CompletedSessionCannotGetOpenDefect),
        ("FONK F-CONC-006 [B-04 düzeltildi]: tamamla / kusur ekle yarışı tamamlanmış+açık kusur üretmez", ViewModelTests.Fonk_CONC006_CompleteAndAddDefectRace),
        ("FONK F-CFG-013 [B-05]: 6 MB not oturumu kilitlemez", ViewModelTests.Fonk_CFG013_HugeNoteDoesNotBrickSession),
        ("FONK F-SESS-008 [B-08 düzeltildi]: '_' ile başlayan parça no reddedilir", ViewModelTests.Fonk_SESS008_UnderscorePartIsRejectedOrVisible),
        ("FONK F-ADM-SET-005 [B-09]: göreli kök yolu reddedilir", AdminViewModelTests.Fonk_SET005_RelativeRootRejected),
        ("FONK F-PHOTO-008 [B-10 düzeltildi]: eksik 01 sonrası yeni foto eklenir, 02 ezilmez", ViewModelTests.Fonk_PHOTO008_PhotoNameCollisionAfterMissingFile),
        ("FONK F-CONC-007 [B-10 düzeltildi]: paralel foto ekleme ikisini de ekler", ViewModelTests.Fonk_CONC007_ParallelPhotoAddsBothSucceed),
        ("FONK F-ADM-TYPE-016 [B-11 düzeltildi]: uygulanmamış tip adı değişikliği kaybolmaz", AdminViewModelTests.Fonk_TYPE016_RenameWithoutApplyIsNotSilentlyLost),
        ("FONK F-CUST-009 [B-12 düzeltildi]: alan türü değişince kayıtlı değer yeniden yazılmaz", ViewModelTests.Fonk_CUST009_FieldKindChangeDoesNotRewriteStoredValue),
        ("FONK F-TAB-006 [B-13 düzeltildi]: ölçüm alanı olmayan tipte sıradaki adım Fotoğraf", ViewModelTests.Fonk_TAB006_TypeWithoutMeasureFieldsGoesToPhoto),
        ("FONK F-EDIT-020 [B-14 düzeltildi]: düzenleyici kaydı kapatılmış kusura yazmaz", ViewModelTests.Fonk_EDIT020_EditorSaveOnDefectClosedByOtherUser),
        ("FONK F-COMP-005 [B-15 düzeltildi]: ikinci Tamamla ikinci denetim satırı yazmaz", ViewModelTests.Fonk_COMP005_SecondCompleteWritesNoSecondAudit),
        ("FONK F-ADM-LOGIN-008 [B-16]: PIN kilidi yeni LoginViewModel'de sürer", AdminViewModelTests.Fonk_LOGIN008_LockoutSurvivesNewLoginViewModel),
        ("FONK F-CONC-013 [B-20 düzeltildi]: eşzamanlı raporlar farklı sürüm alır", ViewModelTests.Fonk_CONC013_ParallelReportsGetDistinctVersions),
        ("FONK F-CFG-005 [B-21 düzeltildi]: tek geçersiz alan tüm ek alanları düşürmez", FonkCoreTests.Fonk_CFG005_OneInvalidFieldDropsOnlyThatField),
        ("FONK F-NET-001 [B-23 düzeltildi]: kök yokken kusur ekleme hata verir, kökü yeniden oluşturmaz", ViewModelTests.Fonk_NET001_RootGoneAddDefectFailsAndDoesNotRecreate),
        ("FONK F-NET-004 [B-23 düzeltildi]: kök yokken rapor hata verir, kökü yeniden oluşturmaz", ViewModelTests.Fonk_NET004_ReportWithRootGoneFailsAndDoesNotRecreate),
        ("FONK F-NET-003 [B-22 düzeltildi]: kök yokken yeni oturum kökü yeniden oluşturmaz", ViewModelTests.Fonk_NET003_RootGoneDoesNotRecreateRoot),
        ("FONK F-NET-006 [B-22 düzeltildi]: kök yokken admin ilk kurulum sunmaz / kökü oluşturmaz", AdminViewModelTests.Fonk_NET006_RootGoneAdminDoesNotOfferFirstRun),
        ("FONK F-CFG-020 [N-2]: kusur olmayan JSON '#0' kusur olarak görünmez", ViewModelTests.Fonk_CFG020_ForeignJsonInDefectsIsIgnored),
    };
}
