// Fonksiyonel test planı Bölüm 6: ŞÜPHELİ (B-01..B-22) testleri. Beklenti DOĞRU davranıştır; kırmızı = hata doğrulandı.
// Normal koşuya girmez; yalnızca: dotnet run --project tests/MuayeneKayit.Tests -- --supheli
public static class FonkSupheliTests
{
    public static List<(string Name, Action Body)> All { get; } = new()
    {
        ("FONK F-MEAS-018 [B-01]: kayıtlı ölçüm otomatik kayıtla yuvarlanmaz (mm, açı, inç, Rework Snapshot)", ViewModelTests.Fonk_MEAS018_RoundTripKeepsPrecision),
        ("FONK F-CFG-008 [B-02]: bozuk admin.json / bozuk Base64 login'i çökertmez", AdminViewModelTests.Fonk_CFG008_CorruptAdminJsonDoesNotCrashLogin),
        ("FONK F-CFG-009 [B-02]: bozuk session.json oturum açmayı çökertmez", ViewModelTests.Fonk_CFG009_CorruptSessionJsonDoesNotCrash),
        ("FONK F-CFG-010 [B-02]: bozuk defects\\N.json oturum/Dashboard/Index'i çökertmez", ViewModelTests.Fonk_CFG010_CorruptDefectFileDoesNotCrash),
        ("FONK F-CFG-011 [B-02]: _audit.log yarım son satır işlemi bozmaz", ViewModelTests.Fonk_CFG011_TruncatedAuditTailDoesNotBreakOperations),
        ("FONK F-CONC-004 [B-03]: eski ekrandaki kullanıcı başkasının ölçümünü ezmez", ViewModelTests.Fonk_CONC004_StaleFormDoesNotOverwriteOtherUsersMeasurement),
        ("FONK F-COMP-004 [B-04]: tamamlanmış oturuma açık kusur eklenemez", ViewModelTests.Fonk_COMP004_CompletedSessionCannotGetOpenDefect),
        ("FONK F-CONC-006 [B-04]: tamamla / kusur ekle yarışı tamamlanmış+açık kusur üretmez", ViewModelTests.Fonk_CONC006_CompleteAndAddDefectRace),
        ("FONK F-CFG-013 [B-05]: 6 MB not oturumu kilitlemez", ViewModelTests.Fonk_CFG013_HugeNoteDoesNotBrickSession),
        ("FONK F-SESS-008 [B-08]: '_' ile başlayan parça no reddedilir ya da görünür", ViewModelTests.Fonk_SESS008_UnderscorePartIsRejectedOrVisible),
        ("FONK F-ADM-SET-005 [B-09]: göreli kök yolu reddedilir", AdminViewModelTests.Fonk_SET005_RelativeRootRejected),
        ("FONK F-PHOTO-008 [B-10]: eksik 01 sonrası yeni foto eklenir, 02 ezilmez", ViewModelTests.Fonk_PHOTO008_PhotoNameCollisionAfterMissingFile),
        ("FONK F-CONC-007 [B-10]: paralel foto ekleme ikisini de ekler", ViewModelTests.Fonk_CONC007_ParallelPhotoAddsBothSucceed),
        ("FONK F-ADM-TYPE-016 [B-11]: uygulanmamış tip adı değişikliği sessizce kaybolmaz", AdminViewModelTests.Fonk_TYPE016_RenameWithoutApplyIsNotSilentlyLost),
        ("FONK F-CUST-009 [B-12]: alan türü değişince kayıtlı değer yeniden yazılmaz", ViewModelTests.Fonk_CUST009_FieldKindChangeDoesNotRewriteStoredValue),
        ("FONK F-TAB-006 [B-13]: ölçüm alanı olmayan tipte sıradaki adım Fotoğraf", ViewModelTests.Fonk_TAB006_TypeWithoutMeasureFieldsGoesToPhoto),
        ("FONK F-EDIT-020 [B-14]: düzenleyici kaydı kapatılmış kusura yazmaz", ViewModelTests.Fonk_EDIT020_EditorSaveOnDefectClosedByOtherUser),
        ("FONK F-COMP-005 [B-15]: ikinci Tamamla ikinci denetim satırı yazmaz", ViewModelTests.Fonk_COMP005_SecondCompleteWritesNoSecondAudit),
        ("FONK F-ADM-LOGIN-008 [B-16]: PIN kilidi yeni LoginViewModel'de sürer", AdminViewModelTests.Fonk_LOGIN008_LockoutSurvivesNewLoginViewModel),
        ("FONK F-ADM-LOGIN-013 [B-19]: admin.json silinince ilk kurulum sunulmaz", AdminViewModelTests.Fonk_LOGIN013_DeletedAdminJsonDoesNotOfferFirstRun),
        ("FONK F-CONC-013 [B-20]: eşzamanlı raporlar farklı sürüm alır", ViewModelTests.Fonk_CONC013_ParallelReportsGetDistinctVersions),
        ("FONK F-CFG-005 [B-21]: tek geçersiz alan tüm ek alanları düşürmez", FonkCoreTests.Fonk_CFG005_OneInvalidFieldDropsOnlyThatField),
        ("FONK F-NET-001 [B-23 YENİ]: kök yokken kusur ekleme hata verir, kökü yeniden oluşturmaz", ViewModelTests.Fonk_NET001_RootGoneAddDefectFailsAndDoesNotRecreate),
        ("FONK F-NET-004 [B-23 YENİ]: kök yokken rapor hata verir, kökü yeniden oluşturmaz", ViewModelTests.Fonk_NET004_ReportWithRootGoneFailsAndDoesNotRecreate),
        ("FONK F-NET-003 [B-22]: kök yokken yeni oturum kökü yeniden oluşturmaz", ViewModelTests.Fonk_NET003_RootGoneDoesNotRecreateRoot),
        ("FONK F-NET-006 [B-22]: kök yokken admin ilk kurulum sunmaz / kökü oluşturmaz", AdminViewModelTests.Fonk_NET006_RootGoneAdminDoesNotOfferFirstRun),
        ("FONK F-CFG-020: kusur olmayan JSON '#0' kusur olarak görünmez", ViewModelTests.Fonk_CFG020_ForeignJsonInDefectsIsIgnored),
    };
}
