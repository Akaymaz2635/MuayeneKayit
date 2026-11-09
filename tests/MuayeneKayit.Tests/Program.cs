using MuayeneKayit.Core;
using MuayeneKayit.Presentation;
using static ViewModelTests;

// Bağımlılıksız test koşucusu. Dış test paketi kullanılmıyor (güncelleme riski yok).
var tests = new List<(string Name, Action Body)>
{
    ("Yol doğrulama: güvenli segmentler kabul edilir", SafePathAcceptsValid),
    ("Yol doğrulama: dolaşım ve ayraç reddedilir", SafePathRejectsTraversal),
    ("Durum: Rework -> Reinspect -> Accept kapatır", StateReworkReinspectAccept),
    ("Durum: Void yalnızca operatör ve yalnızca Open durumda", StateVoidRules),
    ("Durum: MRB reddi otomatik hurda yazar", StateMrbRejectedScraps),
    ("Durum: Vendor'da kusur açık kalır", StateVendorIsOpen),
    ("Durum: Yetkisiz kaynak reddedilir", StateSourceRules),
    ("Geçit: Açık kusur muayeneyi kapatmaz", GateBlocksOpen),
    ("Geçit: Parent-child zinciri çözülünce geçer", GateSupersedeResolves),
    ("Cümle: Defect cümlesi doğru biçimde üretilir", SentenceFormat),
    ("Depo: Numara void olsa da yeniden kullanılmaz", StoreIdsNeverReused),
    ("Depo: Oturum var ise üzerine yazılmaz", StoreOpenOrCreateKeepsExisting),
    ("Index: Rebuild satırları üretir", IndexRebuildsRows),
    ("Denetim: Zincir doğrulanır ve kurcalama yakalanır", AuditChainDetectsTamper),
    ("Doğrulama: Ölçüm değerleri pozitif ve açı aralığında", ValidatorRules),
    ("Fotoğraf: Geçerli JPG kopyalanır, sahte uzantı reddedilir", PhotoImportRules),
    ("Yeniden açma: not zorunlu, geçmiş korunur", ReopenRules),
    ("Rework döngüsü: aynı kusur devam ederse tekrar rework", SameDefectLoops),
    ("Karşılaştırma: artış ve azalış doğru yazılır", CompareDirections),
    ("Void: karar geçmişi olan kusur void edilemez", VoidOnlyWithoutHistory),
    ("Void: gerekçe notu zorunlu", VoidNeedsNote),
    ("Ölçüm düzeltmesi: önce/sonra kaydı tutulur, değişmezse yazılmaz", MeasurementEditsLogged),
    ("Dashboard: parça, seri, hata tipi ve duruma göre süzme", DashboardFilter),
    ("SENARYO 1: Rework sonrası aynı kusur, değer artar, ikinci rework uygun", Scenario1_SameDefectThenConforms),
    ("SENARYO 2: Undercut giderildi, Dent oluştu (child)", Scenario2_NewDefectChild),
    ("SENARYO 3: Kapalı kusur yeniden açılır, hurda açılamaz", Scenario3_ReopenAndScrapFinal),
    ("SENARYO 4: MRB reddi otomatik hurda, muayene kapanabilir", Scenario4_MrbRejected),
    ("Yeniden açma: hurda kesindir", ScrapIsFinal),
    ("Child: Rework sonrası devam yeni kusur açar ve bağlar", ContinueAsChildLinks),
    ("Rapor: Kullanıcı metni HTML olarak kaçılır, void dışarıda kalır", ReportEscapesAndHidesVoid),
    ("İşaretleme: koordinat ve kusur no doğrulanır", MarkupValidation),
    ("İşaretleme: rapor kutu/ok/daire ve #no çizer, temiz kopyayı kullanır", ReportDrawsMarkup),
    ("Arka plan: parça ayrılır, zemin silinir", BackgroundSeparatesPart),
    ("Arka plan: temiz kopya orijinali ve numaralamayı bozmaz", CleanedCopyKeepsOriginal),
    ("Rapor: son sürüm numarası bulunur", ReportVersionRules),
    ("Kurulum ayarı üst klasörlerde aranır (ağdan çalıştırma)", InstallSettingsFoundInParentFolders),
    ("Başlatıcı: surum.txt ve mimariye göre exe seçer", ProgramLayoutResolvesVersionAndArch),
    ("Admin: kusur tipi şablonu kaydedilir, bozuksa varsayılan", DefectTypeTemplates),
    ("Admin: PIN özetle saklanır ve doğrulanır", AdminPinRules),
    ("Admin: eski olmayan ya da kök dışı kilit silinmez", StaleLockRules),
    ("CSV: formül enjeksiyonu etkisizleştirilir", CsvEscapes),
    ("Geri al: son karar geri alınır, kayıt silinmez", UndoRestoresPreviousState),
    ("Geri al: MRB reddi otomatik hurdayla birlikte geri alınır", UndoMrbRejectedRemovesAutoScrap),
    ("Geri al: child devri geri alınır, child void olur", UndoChildLinkVoidsChild),
    ("Admin: karar açıklamaları kaydedilir, eksik olan varsayılan", DecisionTextRules),
    ("Rapor: her fotoğraf ayrı sayfa (6.1, 6.2), geçmiş ilk sayfada", ReportPhotoPerPage),
    ("Kamera çerçevesi: ortalanmış kare, varsayılan kısa kenarın %20'si, boyut ayarlanabilir", CameraFrameSquare),
    ("High metal: cümle, rapor başlığı ve eski kayıt uyumu", HighMetalSentenceAndReport),
    ("High metal: yalnızca işaretlemek düzeltme kaydı oluşturur", HighMetalEditLogged),
    ("SENARYO 5: Yalnızca high metal rework'ü, ana kusur MRB'ye gider", Scenario5_HighMetalOnlyRework),
    ("VM: yeni oturum açılır, başlık ve Kusur ekle vurgusu", VmOpensNewSession),
    ("VM: kusur eklenince seçilir, Ölçüm sekmesi ve Kaydet vurgulu", VmAddDefectSelectsMeasure),
    ("VM: ölçüm kaydı Fotoğraf sekmesine geçer, ✓ ve kamera vurgusu", VmSaveMeasurementsGoesToPhoto),
    ("VM: geçersiz ölçüm hata bildirir, dosya değişmez", VmInvalidMeasurementNotifiesError),
    ("VM: high metal tipte ve cümlede görünür", VmHighMetalShowsInTypeAndSentence),
    ("VM: oturum açıldıktan sonra ilk seçilen kusurda sekme sıradaki adımdır", VmFirstSelectionAfterOpenUsesCurrentStep),
    ("VM: aynı kusur yenilenince sekme korunur", VmRefreshKeepsSelectedStep),
    ("VM: liste yenilenince ListView yeniden seçse de sekme korunur", VmListViewReselectAfterRefreshKeepsTab),
    ("VM: Disposition sekmesinde ✓ yalnızca karar sonrası", VmDecisionTabCheckAfterDecision),
    ("VM: Complete açık kusurda uyarır, kapanınca tamamlar", VmCompleteRules),
    ("VM: karar seçenekleri duruma göre, rework bekleyende ilk seçenek reinspect", VmDecisionOptions),
    ("VM: geri alma metni karar sonrası dolar", VmUndoTextAfterDecision),
    ("VM: başka muayeneye geçince oturum kapanır, liste boşalır", VmChangeSessionClears),
    ("VM: karar kabul kapatır, vazgeç değişiklik yapmaz", VmDecisionAcceptClosesAndCancelChangesNothing),
    ("VM: high metal'li rework kapsam sorar, 'sonra' bekletir", VmReworkHighMetalAsksScopeAndWaits),
    ("VM: rework sonrası 'şimdi gir' sonuç penceresini açar", VmReworkNowOpensReinspectDialog),
    ("VM: reinspect aynı kusur, high metal giderildi, derinlik korunur", VmReinspectSameHighMetalGoneKeepsDepth),
    ("VM: reinspect yeni hata child açar ve seçer", VmReinspectNewOpensChild),
    ("VM: reinspect giderildi kapatır, bekleme dışında hata verir", VmReinspectConformsAndWrongStateErrors),
    ("VM: child devrini geri alma child'ı void eder", VmUndoChildTransferVoidsChild),
    ("VM: void gerekçe ister", VmVoidNeedsReason),
    ("VM: fotoğraf kaldırma onay ister", VmRemovePhotoConfirmation),
    ("VM: ölçümsüz fotoğraf adımına geçişte uyarı", VmPhotoStepWithoutMeasurementAsks),
    ("VM: otomatik kayıt (sekme korunur, geçmiş birleşir, geçersiz değer yazılmaz)", VmAutoSaveMergesAndKeepsStep),
    ("VM: muayeneci Windows oturumundan (aç / kapa, sicil no yedeği)", VmInspectorFromWindowsUser),
    ("VM: kusur eklenince / değişince önceki form kaydedilir", VmAutoSaveOnDefectSwitch),
    ("Ölçüm düzeltmesi birleştirme kuralları", MeasurementEditMergeRules),
    ("VM: reopen'da not zorunlu", VmReopenRequiresNote),
    ("Diyalog kuralları: Merge, CanSave, DeltaText, DecisionForm", DialogFormRules),
    ("VM: fotoğraf kartları etiket, temiz kopya ve bilgi metni", VmPhotoCardsShowLabelsAndCleanCopy),
    ("VM: kapalı kusurda kartlar yalnızca görüntüleme", VmPhotoCardsAreViewOnlyWhenClosed),
    ("VM: dosyadan ekle (vazgeç, ekle, geçersiz dosya)", VmAddPhotoFromFile),
    ("VM: kamera çerçeveli kayıt, kapanınca Fotoğraf sekmesi ve düzenleyici", VmCameraSavesFramedMarkupAndOpensEditor),
    ("VM: kamera kayıt olmadan kapanırsa düzenleyici açılmaz", VmCameraClosedWithoutPhotoDoesNothing),
    ("VM: düzenleyici kaydı işaret ve temiz kopya yazar, boş işaret kaldırılır", VmEditorSaveWritesMarkupAndCleanCopy),
    ("VM: kapalı kusurda düzenleyici yerine dosya açılır", VmOpenEditorOnClosedDefectOpensFile),
    ("VM: rapor sürüm artırır, HTML yazar, PDF açar", VmReportWritesHtmlAndOpensPdf),
    ("VM: PDF başarısızsa HTML açılır ve uyarı verilir", VmReportFallsBackToHtml),
    ("VM: oturumsuz rapor hata verir", VmReportWithoutSessionErrors),
    ("VM: öneriler _ klasörlerini atlar, üst alana göre gelir", VmSuggestionsListFoldersAndSkipUnderscore),
    ("Dashboard VM: süzgeçler ve özet kartları", DashboardViewModelTests.DashEmptyBeforeRefreshThenFiltersAndSummary),
    ("Dashboard VM: boş metinleri ve süzgeçleri temizle", DashboardViewModelTests.DashEmptyTextsAndClearFilters),
    ("Dashboard VM: kart rozetleri ve Aç olayı", DashboardViewModelTests.DashRowCardsCarryChipsAndOpenEvent),
    ("Dashboard VM: denetim doğrulama sağlam ve bozuk", DashboardViewModelTests.DashVerifyAuditIntactAndBroken),
    ("Admin VM: ilk kurulumda PIN eşleşmeli, yazılır ve denetlenir", AdminViewModelTests.FirstRunPinMustMatchAndIsAudited),
    ("Admin VM: kısa PIN kural hatası verir", AdminViewModelTests.ShortPinShowsRuleError),
    ("Admin VM: Windows kimliği anahtarı ortak ayara yazılır", AdminViewModelTests.WindowsUserSwitchSavesSharedSetting),
    ("Admin VM: 5 hatalı PIN 60 sn bekletir, süre dolunca girilir", AdminViewModelTests.FiveWrongPinsWaitSixtySecondsThenRetry),
    ("Admin VM: başarılı girişten sonra hatalı sayaç sıfırlanır", AdminViewModelTests.WrongPinCounterResetsAfterSuccess),
    ("Admin VM: 10 dk girdi yoksa boşta kilit", AdminViewModelTests.IdleLockAfterTenMinutesOfNoInput),
    ("Admin VM: gezinme sayfaları açar, yeniler ve kilitler", AdminViewModelTests.NavigationPagesLockAndRefresh),
    ("Admin VM: tip ekle, uygula, taşı, sil (onay), kaydet", AdminViewModelTests.TypesAddApplyMoveDeleteSave),
    ("Admin VM: tipleri varsayılana döndürme onay ister", AdminViewModelTests.TypesResetToDefaultsAsksConfirm),
    ("Admin VM: açıklama düzenle, kaydet, sıfırla", AdminViewModelTests.TextsEditSaveAndResetAll),
    ("Admin VM: eski kilit silme onay ister ve denetlenir", AdminViewModelTests.MaintenanceStaleLockRemovalNeedsConfirm),
    ("Admin VM: index yenileme ve denetim doğrulama", AdminViewModelTests.MaintenanceRebuildAndVerifyAudit),
    ("Admin VM: kayıt süzgeçleri, özet ve kartlar", AdminViewModelTests.RecordsFiltersSummaryAndCards),
    ("Admin VM: CSV dışa aktarım BOM ve süzülmüş satırlar", AdminViewModelTests.RecordsExportCsvWritesBomAndFilteredRows),
    ("Admin VM: klasör ve son rapor açma", AdminViewModelTests.RecordsOpenFolderAndLatestReport),
    ("Admin VM: ortak alan değişince oturum kilitlenir", AdminViewModelTests.SettingsRootMustExistThenLocksSession),
    ("Admin VM: PIN değiştirme eski ve tekrar PIN'i doğrular", AdminViewModelTests.SettingsChangePinValidatesOldAndRepeat),
    ("Eşzamanlı: iki muayeneci aynı anda kusur ekler, numaralar tekil", ConcurrencyTests.ParallelAddDefectsGetUniqueIds),
    ("Eşzamanlı: aynı kusura paralel güncelleme kaybolmaz", ConcurrencyTests.ParallelUpdatesSameDefectLoseNothing),
    ("Eşzamanlı: paralel denetim kaydı zinciri bozmaz", ConcurrencyTests.ParallelAuditAppendsKeepChain),
    ("Birim: inch .123 biçimi, mm 2 ondalık, ek ve başlık", UnitsFormatRules),
    ("Birim: cümle inch/mm ve açı etkilenmez", UnitSentenceInchAndMm),
    ("Birim: eski JSON mm okunur, inch yazılıp okunur", UnitJsonOldAndRoundTrip),
    ("Birim: karşılaştırma inch biçimi", UnitCompareInch),
    ("Birim VM: kusur ayardaki birimle açılır, mevcut kusur kalır", VmUnitFollowsSettingsPerDefect),
    ("Birim VM: kusur yokken başlıklar ayara uyar", VmUnitHeadersFollowSettingsWhenNoDefect),
    ("Birim: .123 ve ,123 (tr-TR) ayrıştırılır", ParseAcceptsLeadingDotAndComma),
    ("Birim: alt kusur ana kusurun birimini alır", ChildDefectInheritsParentUnit),
    ("Ayarlar VM: tema ve birim yazılır, olay gelir", UserSettingsViewModelWritesAndRaises),
    ("Birim: rapor HTML inch değerleri gösterir", ReportShowsInchValues),
    ("Geri bildirim: çoklu yeni hata (aynı tip ×2 + farklı tip), cümle ve geçit", FeedbackCoreTests.MultipleNewDefectsLinkAndGate),
    ("Geri bildirim: bölgeli karışık sonuç (giderildi / devam / yeni hata ×2), ikinci turda kapanır", FeedbackCoreTests.MixedRegionOutcomes),
    ("Geri bildirim: hepsi giderildi kapanır, kusurda olmayan bölge reddedilir", FeedbackCoreTests.AllRegionsConformClosesAndInvalidPhotoRejected),
    ("Geri bildirim: geri alma tüm child'ları void eder, bölge durumları geri gelir", FeedbackCoreTests.UndoVoidsAllChildrenAndRestoresRegions),
    ("Geri bildirim: son child void edilince parent geri açılır, hiçbir yerde sızmaz", FeedbackCoreTests.VoidLastChildReopensParent),
    ("Geri bildirim: birden çok child'dan biri void, parent devredilmiş kalır", FeedbackCoreTests.VoidOneOfManyChildrenKeepsParentTransferred),
    ("Geri bildirim: çoklu child'ın sonuncusu void edilince parent geri açılır", FeedbackCoreTests.VoidLastOfManyChildrenReopensParent),
    ("Geri bildirim: 'aynı kusur' kararındaki child void edilince parent açık kalır", FeedbackCoreTests.VoidChildOfSameDefectDecisionKeepsParentOpen),
    ("Geri bildirim: eski supersededById JSON'u aynen devredilmiş görünür", FeedbackCoreTests.LegacySupersededByIdStillReadable),
    ("Geri bildirim: eski kayıtta devir geri alınınca eski bağ çözülür", FeedbackCoreTests.LegacyUndoClearsOldLink),
    ("Geri bildirim: yeni alanlar yazılıp okunur, eski JSON aynen yüklenir", FeedbackCoreTests.NewFieldsRoundTripAndOldJsonLoads),
    ("Geri bildirim: bölge durumu kararlardan türer (undo, reopen, yeni kusur numarası)", FeedbackCoreTests.RegionStatusFollowsEffectiveDecisionsAndReopen),
    ("Geri bildirim: rapor bölge satırları ve çoğul bağlantılar", FeedbackCoreTests.ReportShowsRegionLinesAndPluralLinks),
    ("Geri bildirim: İngilizce Inspector ve Çap / Diameter metinleri", FeedbackCoreTests.EnglishInspectorAndDiameterTexts),
    ("Geri bildirim: reinspect / karar penceresi bölge kuralları", FeedbackCoreTests.ReinspectFormRegionRules),
    ("Geri bildirim: gizlenen öneri kümesi (büyük/küçük harf, JSON, bozuk dosya)", FeedbackCoreTests.HiddenSuggestionSetRulesAndJson),
    ("Geri bildirim VM: öneri gizleme kutuya özel, kullanılınca geri gelir", VmHiddenSuggestionsPerBoxAndReturnOnUse),
    ("Geri bildirim VM: Inspector ve Çap metinleri", VmEnglishInspectorTexts),
    ("Geri bildirim VM: reinspect birden fazla yeni hata açar", VmReinspectMultipleNewDefects),
    ("Geri bildirim 3: açıklama cümlesi (artikel, ölçü kısaltmaları, high metal, inch)", PartListTests.StatementArticleAndMeasurements),
    ("Geri bildirim 3: cümlede parça kapsamı ve eski kayıt", PartListTests.StatementPartScopeAndOldRecord),
    ("Geri bildirim 3: PartList Parse / Normalize / Describe", PartListTests.PartListParseNormalizeDescribe),
    ("Geri bildirim 3: PartList geçersiz girdiyi reddeder", PartListTests.PartListRejectsInvalid),
    ("Geri bildirim 3: rework sonrası child parçaları devralır", PartListTests.ChildInheritsAffectedParts),
    ("Geri bildirim 3: rapor açıklama bölümü ve etkilenen parçalar satırı", PartListTests.ReportNonconformanceAndPartsLines),
    ("Geri bildirim 3: CSV etkilenen parçalar sütunu", PartListTests.CsvHasAffectedPartsColumn),
    ("Geri bildirim 3 VM: etkilenen parçalar otomatik kayıt, geçersiz girdi", VmAffectedPartsAutoSave),
    ("Geri bildirim VM: tipi seçilmemiş yeni hata işlenmez", VmReinspectNewDefectWithoutTypeIsRejected),
    ("Geri bildirim VM: karar penceresi bölge sorar, işaretsiz uygun sayılır, undo geri getirir", VmDecisionAsksRegionsAndMarksUncheckedConforming),
    ("Geri bildirim VM: karar bölge seçimi doğrulanır", VmDecisionRegionSelectionIsValidated),
    ("Geri bildirim VM: bölge bazında reinspect, rozet, geçmiş, sonraki tur yalnızca aktif", VmReinspectPerRegionOutcomes),
    ("Geri bildirim VM: eksik bölge seçimi işlenmez, hepsi giderilince kapanır", VmReinspectRegionsMissingChoiceIsRejected),
    ("Geri bildirim VM: void child parent'ı geri açar, ekranda sızıntı yok", VmVoidLastChildReopensParentWithoutLeaks),
    ("Geri bildirim VM: iki child'dan biri void, sonra geri alma", VmUndoMultipleChildrenAndVoidOneOfTwo),
    ("Geri bildirim VM: geri alma iki child'ı bildirir ve void eder", VmUndoTwoChildrenListsBoth),
    ("Rapor 2: tarihler ay harfli, ay-gün-yıl, kültürden bağımsız", ReportBilingualTests.ReportDatesAreInvariantMonthDayYear),
    ("Rapor 2: başlık tek satır iki dilli, Proje / Inspector / sürüm / durum yok", ReportBilingualTests.HeaderIsSingleBilingualLine),
    ("Rapor 2: alt bilgide muayene açılış tarihi", ReportBilingualTests.FooterShowsSessionOpeningDate),
    ("Rapor 2: tablo tarihleri MMM.dd.yyyy HH:mm", ReportBilingualTests.TableDatesUseMonthNames),
    ("Rapor 2: kusur sayfası etiketleri Türkçe / English", ReportBilingualTests.DefectPageLabelsAreBilingual),
    ("Rapor 2: durum ve karar adları iki dilli, sistem notu çevrilir", ReportBilingualTests.StateAndDecisionNamesAreBilingual),
    ("Rapor 2: bağlantı cümleleri ve bölge satırları iki dilli", ReportBilingualTests.LinkSentencesAndRegionLinesAreBilingual),
    ("Ek ölçüm: alan tanımı kaydedilir, geçersiz / yerleşik kısaltma reddedilir", CustomFieldTests.FieldDefinitionsSaveAndValidate),
    ("Ek ölçüm: cümle, rapor, karşılaştırma ve doğrulama", CustomFieldTests.SentenceReportCompareValidate),
    ("Ek ölçüm: düzeltme kaydı, ad farkı değişiklik sayılmaz, eski kayıt", CustomFieldTests.EditorEqualityAndOldRecords),
    ("Ek ölçüm VM: şablondaki kutu çıkar, kaydedilir, cümleye yazılır", ViewModelTests.VmCustomFieldShownSavedAndInSentence),
    ("Ek ölçüm VM: negatif uzunluk reddedilir, sayı türünde eksi kabul", ViewModelTests.VmCustomFieldValidation),
    ("Ek ölçüm VM: şablon dışı / silinmiş alandaki değer gizlenmez", ViewModelTests.VmCustomFieldOrphanValueStaysVisible),
    ("Ek ölçüm VM: rework sonrası boş bırakılan ek ölçüm korunur", ViewModelTests.VmReinspectMergesCustomField),
    ("Ek ölçüm Admin: alan ekle, tipe işaretle, kaydet, sil", AdminViewModelTests.TypesCustomFieldsAddCheckSaveDelete),

    // ───── Güvenlik testleri: bugün geçen (regresyon) davranışlar. Bilinen açıklar aşağıdaki openFindings listesinde ─────
    ("Güvenlik S-PATH-002: mutlak / sürücü / UNC / ADS segmentleri reddedilir", SecurityTests.SegmentsRejectAbsoluteDriveUncAds),
    ("Güvenlik S-PATH-004: sonda nokta ve görünmez karakter reddedilir", SecurityTests.SegmentsRejectTrailingDotAndInvisibleChars),
    ("Güvenlik S-PATH-005: segment uzunluk sınırı", SecurityTests.SegmentLengthLimit),
    ("Güvenlik S-PATH-015: içe aktarılan dosya adı kaynaktan bağımsız", SecurityTests.ImportedFileNameIsIndependentOfSource),
    ("Güvenlik S-HTML-002: tüm kullanıcı alanları kaçılır", SecurityTests.ReportEscapesAllUserFields),
    ("Güvenlik S-HTML-003: öznitelik bağlamı kırılmaz", SecurityTests.ReportAttributeContextIsEscaped),
    ("Güvenlik S-HTML-004: geçersiz sayılar SVG'ye yazılmaz", SecurityTests.ReportInvalidNumbersDoNotReachSvg),
    ("Güvenlik S-HTML-005: işaret sayısı sınırlı ve hızlı", SecurityTests.ReportShapeCountIsBounded),
    ("Güvenlik S-HTML-006: geçersiz CleanedFile raporda kullanılmaz", SecurityTests.ReportIgnoresInvalidCleanedFile),
    ("Güvenlik S-HTML-007: <title> kaçılır", SecurityTests.ReportTitleIsEscaped),
    ("Güvenlik S-HTML-008: ek ölçüm adı / değeri kaçılır", SecurityTests.ReportCustomMeasureNamesEscaped),
    ("Güvenlik S-HTML-009: bölge etiketi ve bağlantı cümleleri kaçılır", SecurityTests.ReportRegionLabelsEscaped),
    ("Güvenlik S-HTML-010: rapor etkin içerik üretmez", SecurityTests.ReportHasNoActiveContent),
    ("Güvenlik S-CSV-002: diğer formül önekleri etkisiz", SecurityTests.CsvFormulaPrefixesNeutralised),
    ("Güvenlik S-CSV-003: baştaki boşluk / tam genişlik işaret gözlemi", SecurityTests.CsvLeadingSpaceAndFullWidthObservation),
    ("Güvenlik S-CSV-004: ayraç / tırnak / satır sonu tutarlı", SecurityTests.CsvSeparatorQuoteNewlineStayConsistent),
    ("Güvenlik S-CSV-006: tüm sütunlar Cell'den geçer", SecurityTests.CsvAllColumnsGoThroughCell),
    ("Güvenlik S-JSON-004: 5 MB üstü session.json reddedilir, mesaj gösterilir", SecurityTests.OversizedSessionFileRejectedWithMessage),
    ("Güvenlik S-JSON-018: gizlenen öneri dosyası bozuk / büyük", SecurityTests.HiddenSuggestionsFromJsonIsTolerant),
    ("Güvenlik S-JSON-019: bozuk kurulum ayarı demo köke düşer", SecurityTests.InstallSettingsCorruptFallsBackToDemoRoot),
    ("Güvenlik S-JSON-020: 10.000 şekil kart listesini yavaşlatmaz", SecurityTests.ManyShapesLoadFastInPhotoCards),
    ("Güvenlik S-PIN-001: PIN düz metin saklanmaz, tuz benzersiz", SecurityTests.PinIsHashedWithUniqueSalt),
    ("Güvenlik S-PIN-003: çevrimdışı deneme maliyeti ölçümü", SecurityTests.PinVerifyCostIsMeasured),
    ("Güvenlik S-PIN-011: PIN biçimi (13 hane, harf, boş, kısa)", SecurityTests.PinFormatValidation),
    ("Güvenlik S-AUD-007: denetim satırı enjeksiyonu yok", SecurityTests.AuditLineInjectionImpossible),
    ("Güvenlik S-AUD-011: muayeneci işlemleri denetlenir", SecurityTests.InspectorActionsAreAudited),
    ("Güvenlik S-LOCK-002: eski kilit kontrollü hata verir", SecurityTests.StaleSessionLockGivesControlledError),
    ("Güvenlik S-LOCK-005: canlı kilit silinmez", SecurityTests.LiveLockCannotBeRemoved),
    ("Güvenlik S-LOCK-006: kök dışı / uzantısız kilit silinmez (kardeş kök dahil)", SecurityTests.RemoveStaleLockRejectsOutsideAndNonLock),
    ("Güvenlik S-LOCK-007: kök kilitleri 15 sn sonra mesaj verir, uygulama çalışır kalır", SecurityTests.RootLocksDoNotHangApplicationForever),
    ("Güvenlik S-LOCK-008: kilit yolu klasörse kontrollü hata", SecurityTests.LockPathThatIsDirectoryGivesControlledError),
    ("Güvenlik S-PHOTO-001: uzantı ve imza denetimi", SecurityTests.ImportRejectsBadExtensionAndSignature),
    ("Güvenlik S-PHOTO-002: dosya boyutu sınırı (25 MB + 1)", SecurityTests.ImportRejectsOversizeFile),
    ("Güvenlik S-PHOTO-007: temiz kopya adı ve içeriği doğrulanır", SecurityTests.WriteCleanedValidatesNameAndContent),
    ("Güvenlik S-PHOTO-008: numara çakışmasında üzerine yazılmaz", SecurityTests.ImportNeverOverwritesExistingPhoto),
    ("Güvenlik S-LAUNCH-001: surum.txt dolaşım / mutlak / UNC / boş reddedilir", SecurityTests.ProgramLayoutRejectsEscapingVersions),
    ("Güvenlik S-LAUNCH-005: ayar dosyası arama derinliği (3 üst)", SecurityTests.InstallSettingsSearchDepth),
    ("Güvenlik S-NTFS-003: salt okunur _config kontrollü hata verir", SecurityTests.ReadOnlyConfigGivesControlledError),
};

// ───── Fonksiyonel test planı (docs/test/FONKSIYONEL-TEST-PLANI.md): yeni testler; ŞÜPHELİ olanlar yalnızca --supheli ile ─────
tests.AddRange(new (string Name, Action Body)[]
{
    ("FONK F-SESS-003: Blank Inspector Rejected", ViewModelTests.Fonk_SESS003_BlankInspectorRejected),
    ("FONK F-SESS-004: Blank Fields Rejected", ViewModelTests.Fonk_SESS004_BlankFieldsRejected),
    ("FONK F-SESS-005: Traversal Parts Rejected", ViewModelTests.Fonk_SESS005_TraversalPartsRejected),
    ("FONK F-SESS-006: Part Length Boundary", ViewModelTests.Fonk_SESS006_PartLengthBoundary),
    ("FONK F-SESS-007: Trim And Turkish Letters", ViewModelTests.Fonk_SESS007_TrimAndTurkishLetters),
    ("FONK F-SESS-002: Second Inspector Keeps Session Inspector", ViewModelTests.Fonk_SESS002_SecondInspectorKeepsSessionInspector),
    ("FONK F-SESS-016: Change Session Saves Pending Text", ViewModelTests.Fonk_SESS016_ChangeSessionSavesPendingText),
    ("FONK F-SESS-018: Long And Special Inspector Names Keep Audit Intact", ViewModelTests.Fonk_SESS018_LongAndSpecialInspectorNamesKeepAuditIntact),
    ("FONK F-DEF-002: Null Type Rejected", ViewModelTests.Fonk_DEF002_NullTypeRejected),
    ("FONK F-DEF-003: Add Defect Without Session", ViewModelTests.Fonk_DEF003_AddDefectWithoutSession),
    ("FONK F-DEF-005: Spec Name Copied", ViewModelTests.Fonk_DEF005_SpecNameCopied),
    ("FONK F-DEF-012: Orphan Type Shows All Fields", ViewModelTests.Fonk_DEF012_OrphanTypeShowsAllFields),
    ("FONK F-MEAS-003: Non Positive Rejected", ViewModelTests.Fonk_MEAS003_NonPositiveRejected),
    ("FONK F-MEAS-004: Na NInfinity Overflow Rejected", ViewModelTests.Fonk_MEAS004_NaNInfinityOverflowRejected),
    ("FONK F-MEAS-005: Tiny And Huge Values", ViewModelTests.Fonk_MEAS005_TinyAndHugeValues),
    ("FONK F-MEAS-006: Angle Range", ViewModelTests.Fonk_MEAS006_AngleRange),
    ("FONK F-MEAS-007: Color Length", ViewModelTests.Fonk_MEAS007_ColorLength),
    ("FONK F-MEAS-009: Thousand Separators Never Silently Wrong", ViewModelTests.Fonk_MEAS009_ThousandSeparatorsNeverSilentlyWrong),
    ("FONK F-MEAS-010: Empty Form Saves Nothing", ViewModelTests.Fonk_MEAS010_EmptyFormSavesNothing),
    ("FONK F-MEAS-012: No Change No Write", ViewModelTests.Fonk_MEAS012_NoChangeNoWrite),
    ("FONK F-MEAS-014: Invalid Text Blocks Auto Save Then Fix Saves", ViewModelTests.Fonk_MEAS014_InvalidTextBlocksAutoSaveThenFixSaves),
    ("FONK F-MEAS-019: Save Without Selection", ViewModelTests.Fonk_MEAS019_SaveWithoutSelection),
    ("FONK F-HM-007: High Metal With Invalid Depth Nothing Written", ViewModelTests.Fonk_HM007_HighMetalWithInvalidDepthNothingWritten),
    ("FONK F-PHOTO-005: Allowed Extensions", ViewModelTests.Fonk_PHOTO005_AllowedExtensions),
    ("FONK F-PHOTO-004: Size Limit Boundary", ViewModelTests.Fonk_PHOTO004_SizeLimitBoundary),
    ("FONK F-PHOTO-006: Picked File Deleted", ViewModelTests.Fonk_PHOTO006_PickedFileDeleted),
    ("FONK F-PHOTO-007: Sequential Names", ViewModelTests.Fonk_PHOTO007_SequentialNames),
    ("FONK F-PHOTO-012: Bad Photo Names Ignored", ViewModelTests.Fonk_PHOTO012_BadPhotoNamesIgnored),
    ("FONK F-PHOTO-014: Missing Clean Copy Shows Original", ViewModelTests.Fonk_PHOTO014_MissingCleanCopyShowsOriginal),
    ("FONK F-RWK-007: Negative Depth On Reinspect Rejected", ViewModelTests.Fonk_RWK007_NegativeDepthOnReinspectRejected),
    ("FONK F-DEC-006: Blank Rework Target Uses Session Operation", ViewModelTests.Fonk_DEC006_BlankReworkTargetUsesSessionOperation),
    ("FONK F-DEC-016: Pending Depth Saved Before Decision", ViewModelTests.Fonk_DEC016_PendingDepthSavedBeforeDecision),
    ("FONK F-DEC-021: Decider And Undoer Are Current User", ViewModelTests.Fonk_DEC021_DeciderAndUndoerAreCurrentUser),
    ("FONK F-UNDO-006: Undo Reopen And Chain", ViewModelTests.Fonk_UNDO006_UndoReopenAndChain),
    ("FONK F-UNDO-008: Nothing To Undo", ViewModelTests.Fonk_UNDO008_NothingToUndo),
    ("FONK F-COMP-006: Pending Text Saved Before Complete", ViewModelTests.Fonk_COMP006_PendingTextSavedBeforeComplete),
    ("FONK F-ID-002: Fake Inspector Text Ignored When Windows Identity On", ViewModelTests.Fonk_ID002_FakeInspectorTextIgnoredWhenWindowsIdentityOn),
    ("FONK F-CONC-005: Stale Screen Decision Rejected", ViewModelTests.Fonk_CONC005_StaleScreenDecisionRejected),
    ("FONK F-NET-002: Root Gone Auto Save Keeps Text Then Saves", ViewModelTests.Fonk_NET002_RootGoneAutoSaveKeepsTextThenSaves),
    ("FONK F-NET-007: Add Photo With Root Gone", ViewModelTests.Fonk_NET007_AddPhotoWithRootGone),
    ("FONK F-RPT-007: Reports Is AFile", ViewModelTests.Fonk_RPT012_ReportsIsAFile),
    ("FONK F-RPT--000: Various Sessions", ViewModelTests.Fonk_RPT_VariousSessions),
    ("FONK F-SET-006: Invalid Indices Ignored", ViewModelTests.Fonk_SET006_InvalidIndicesIgnored),
    ("FONK F-CAM-011: Camera Target Is Fixed To Original Defect", ViewModelTests.Fonk_CAM011_CameraTargetIsFixedToOriginalDefect),
    ("FONK F-EDIT-017: Editor Save Goes To Original Defect", ViewModelTests.Fonk_EDIT017_EditorSaveGoesToOriginalDefect),
    ("FONK F-COMP-001: Open Defects Block Complete", ViewModelTests.Fonk_COMP001_OpenDefectsBlockComplete),
    ("FONK F-E2E-009: Repair Paths", ViewModelTests.Fonk_E2E009_RepairPaths),
    ("FONK F-E2E-001: Full Happy Path", ViewModelTests.Fonk_E2E001_FullHappyPath),
    ("FONK F-E2E-007: Void Wrong Type Then Dent", ViewModelTests.Fonk_E2E007_VoidWrongTypeThenDent),
    ("FONK F-E2E-008: Vendor Flow", ViewModelTests.Fonk_E2E008_VendorFlow),
    ("FONK F-E2E-016: Reopen Then Rework Conforms", ViewModelTests.Fonk_E2E016_ReopenThenReworkConforms),
    ("FONK F-E2E-005: Mrb Rejected Flow", ViewModelTests.Fonk_E2E005_MrbRejectedFlow),
    ("FONK F-ADM-LOGIN-014: Weak Iterations Rejected", AdminViewModelTests.Fonk_LOGIN014_WeakIterationsRejected),
    ("FONK F-ADM-LOGIN-003: Pin Format Rules", AdminViewModelTests.Fonk_LOGIN003_PinFormatRules),
    ("FONK F-ADM-LOGIN-013: Behaviour_Info", AdminViewModelTests.Fonk_LOGIN013_Behaviour_Info),
    ("FONK F-ADM-TYPE-003: Name Rules", AdminViewModelTests.Fonk_TYPE003_NameRules),
    ("FONK F-ADM-TYPE-004: Spec Length", AdminViewModelTests.Fonk_TYPE004_SpecLength),
    ("FONK F-ADM-TYPE-005: Apply Without Selection", AdminViewModelTests.Fonk_TYPE005_ApplyWithoutSelection),
    ("FONK F-ADM-TYPE-006: No Measure Fields Allowed", AdminViewModelTests.Fonk_TYPE006_NoMeasureFieldsAllowed),
    ("FONK F-ADM-TYPE-009: Delete All Then Save Rejected", AdminViewModelTests.Fonk_TYPE009_DeleteAllThenSaveRejected),
    ("FONK F-ADM-FIELD-006: Kind Index Mapping", AdminViewModelTests.Fonk_FIELD006_KindIndexMapping),
    ("FONK F-ADM-FIELD-007: Apply Field Rules", AdminViewModelTests.Fonk_FIELD007_ApplyFieldRules),
    ("FONK F-ADM-TXT-002: Decision Text Rules", AdminViewModelTests.Fonk_TXT002_DecisionTextRules),
    ("FONK F-ADM-MNT-003: Audit Load Limits And Skips Bad Line", AdminViewModelTests.Fonk_MNT003_AuditLoadLimitsAndSkipsBadLine),
    ("FONK F-ADM-MNT-009: Nine Minute Lock Not Stale", AdminViewModelTests.Fonk_MNT009_NineMinuteLockNotStale),
    ("FONK F-ADM-SET-003: Blank Root Rejected", AdminViewModelTests.Fonk_SET003_BlankRootRejected),
    ("FONK F-ADM-SET-004: Env Var Root Stored Raw", AdminViewModelTests.Fonk_SET004_EnvVarRootStoredRaw),
    ("FONK F-ADM-REC-003: Many Sessions Cap", AdminViewModelTests.Fonk_REC003_ManySessionsCap),
    ("FONK F-ADM-REC-005: Export Cancel And Read Only Target", AdminViewModelTests.Fonk_REC005_ExportCancelAndReadOnlyTarget),
    ("FONK F-ADM-REC-010: Records Page Only Touches Index And Audit", AdminViewModelTests.Fonk_REC010_RecordsPageOnlyTouchesIndexAndAudit),
    ("FONK F-CFG-021: No Config Folder Defaults", AdminViewModelTests.Fonk_CFG021_NoConfigFolderDefaults),
    ("FONK F-ADM-LOGIN-009: Idle Lock Seconds", AdminViewModelTests.Fonk_LOGIN009_IdleLockSeconds),
    ("FONK F-SM-001: Full Matrix", FonkCoreTests.Fonk_SM001_FullMatrix),
    ("FONK F-SM-002: Source Matrix", FonkCoreTests.Fonk_SM002_SourceMatrix),
    ("FONK F-SM-005: Reinspect Has No Snapshot", FonkCoreTests.Fonk_SM005_ReinspectHasNoSnapshot),
    ("FONK F-SM-004: Rework Target Required", FonkCoreTests.Fonk_SM004_ReworkTargetRequired),
    ("FONK F-SM-006: Repairing Transitions", FonkCoreTests.Fonk_SM006_RepairingTransitions),
    ("FONK F-SM-010: After Rework Transitions", FonkCoreTests.Fonk_SM010_AfterReworkTransitions),
    ("FONK F-SM-013: Scope And New Ids Only Where Allowed", FonkCoreTests.Fonk_SM013_ScopeAndNewIdsOnlyWhereAllowed),
    ("FONK F-SM-014: Voided And Superseded Reject Everything", FonkCoreTests.Fonk_SM014_VoidedAndSupersededRejectEverything),
    ("FONK F-SM-019: Undo With Children Outside Store Rejected", FonkCoreTests.Fonk_SM019_UndoWithChildrenOutsideStoreRejected),
    ("FONK F-SM-018: Void Not Undoable", FonkCoreTests.Fonk_SM018_VoidNotUndoable),
    ("FONK F-PART-003: Part List Accept Reject", FonkCoreTests.Fonk_PART003_PartListAcceptReject),
    ("FONK F-CFG-001: Missing Settings File Falls Back To Demo Path", FonkCoreTests.Fonk_CFG001_MissingSettingsFileFallsBackToDemoPath),
    ("FONK F-CFG-002: Bad Settings File Falls Back", FonkCoreTests.Fonk_CFG002_BadSettingsFileFallsBack),
    ("FONK F-CFG-003: Env Expansion And Parent Levels", FonkCoreTests.Fonk_CFG003_EnvExpansionAndParentLevels),
    ("FONK F-CFG-007: Bad Identity File Means Manual", FonkCoreTests.Fonk_CFG007_BadIdentityFileMeansManual),
    ("FONK F-CFG-004: Bad Types File Falls Back To Nine", FonkCoreTests.Fonk_CFG004_BadTypesFileFallsBackToNine),
    ("FONK F-ADM-FIELD-002: Name Rules", FonkCoreTests.Fonk_FIELD002_NameRules),
    ("FONK F-ADM-FIELD-003: Abbrev Rules", FonkCoreTests.Fonk_FIELD003_AbbrevRules),
    ("FONK F-ADM-FIELD-004: Duplicate Name And Abbrev", FonkCoreTests.Fonk_FIELD004_DuplicateNameAndAbbrev),
    ("FONK F-ADM-FIELD-005: Max Thirty Fields", FonkCoreTests.Fonk_FIELD005_MaxThirtyFields),
    ("FONK F-ADM-TYPE-010: Max Hundred Types", FonkCoreTests.Fonk_TYPE010_MaxHundredTypes),
    ("FONK F-ADM-SET-006: Too Long Root Path Message", FonkCoreTests.Fonk_SET006_TooLongRootPathMessage),
    ("FONK F-LNC-002: Missing Version File", FonkCoreTests.Fonk_LNC002_MissingVersionFile),
    ("FONK F-LNC-003: Invalid Version Texts", FonkCoreTests.Fonk_LNC003_InvalidVersionTexts),
    ("FONK F-LNC-004: Version Folder Missing", FonkCoreTests.Fonk_LNC004_VersionFolderMissing),
    ("FONK F-LNC-006: Version File First Line And Bom", FonkCoreTests.Fonk_LNC006_VersionFileFirstLineAndBom),
    ("FONK F-LNC-008: Save Root Path Next To Version File", FonkCoreTests.Fonk_LNC008_SaveRootPathNextToVersionFile),
    ("FONK F-CONC-008: Lock Held Five Seconds Then Acquired", FonkCoreTests.Fonk_CONC008_LockHeldFiveSecondsThenAcquired),
});
if (args.Contains("--supheli"))
    tests.AddRange(FonkSupheliTests.All);

// Güvenlik testleri (docs/test/GUVENLIK-TEST-PLANI.md). Bugün başarısız olması beklenen, bilinen açık bulguları
// belgeleyen testler burada: başarısızlık "AÇIK BULGU" yazar ve çıkış kodunu ETKİLEMEZ; geçerse "DÜZELDİ" yazar
// (normal listeye taşıyın).
var openFindings = new List<(string Id, string Name, Action Body)>
{
    ("ÖB-13", "S-PATH-003 ayrılmış aygıt adları (CON, NUL, COM1...) reddedilir", SecurityTests.SegmentsRejectReservedDeviceNames),
    ("ÖB-13", "S-PATH-006 kök içindeki junction izlenmez", SecurityTests.JunctionInsideRootIsNotFollowed),
    ("ÖB-16", "S-PATH-007 admin klasörü aç kardeş klasöre (Veri2) çıkmaz", SecurityTests.AdminOpenFolderRejectsSiblingPrefix),
    ("ÖB-16", "S-PATH-008 admin son rapor kök altında kalır", SecurityTests.AdminOpenLatestReportStaysUnderRoot),
    ("ÖB-01", "S-PATH-010 kapalı kusurda CleanedFile kusur klasörü dışına çıkmaz", SecurityTests.ClosedDefectCleanedFileStaysInDefects),
    ("ÖB-01", "S-PATH-011 mutlak / UNC CleanedFile reddedilir", SecurityTests.ClosedDefectAbsoluteAndUncCleanedFileRejected),
    ("ÖB-02", "S-PATH-012 Photos adları kusur klasörüne bağlıdır", SecurityTests.PhotoNamesTraversalDoesNotEscapeDefectsFolder),
    ("ÖB-02", "S-PATH-013 UNC fotoğraf adı yol üretmez", SecurityTests.PhotoNameUncDoesNotProducePath),
    ("ÖB-02", "S-PATH-014 rapor görsel kaynağı kusur klasöründe kalır", SecurityTests.ReportPhotoSourceStaysInDefectsFolder),
    ("ÖB-09", "S-HTML-011 raporda CSP var", SecurityTests.ReportHasContentSecurityPolicy),
    ("ÖB-09", "S-HTML-014 admin son raporda paylaşımdaki HTML'i açmaz", SecurityTests.AdminLatestReportDoesNotOpenHtmlOnShare),
    ("YENİ", "S-CSV-005 virgül ayraçlı yerel ayarda formül hücresi oluşmaz", SecurityTests.CsvCommaDelimitedLocaleDoesNotCreateFormulaCell),
    ("ÖB-03", "S-JSON-001 yarım session.json dashboard'u düşürmez", SecurityTests.DashboardSurvivesTruncatedSession),
    ("ÖB-03", "S-JSON-002 bozuk defects\\N.json muayene ekranını düşürmez", SecurityTests.CorruptDefectDoesNotCrashInspection),
    ("ÖB-03", "S-JSON-003 null koleksiyonlar boş liste gibi davranır", SecurityTests.NullCollectionsBehaveLikeEmpty),
    ("ÖB-03", "S-JSON-005 derin iç içe JSON VM'de yakalanır", SecurityTests.DeepNestingIsHandledByViewModel),
    ("ÖB-03", "S-JSON-006 aralık dışı enum sayısı reddedilir", SecurityTests.EnumOutOfRangeRejected),
    ("ÖB-03", "S-JSON-007 bilinmeyen enum metni VM'de yakalanır", SecurityTests.EnumUnknownTextHandledByViewModel),
    ("ÖB-03", "S-JSON-008 index yeniden üretimi tek bozuk kayıtla durmaz", SecurityTests.IndexRebuildSurvivesOneCorruptSession),
    ("ÖB-03", "S-JSON-009 admin Kayıtlar bozuk kayıtla açılır", SecurityTests.AdminRecordsOpensWithCorruptSession),
    ("ÖB-03", "S-JSON-010 kusur tipi şablonu bozuk / null varyantları", SecurityTests.DefectTypeTemplateFileVariantsFallBackToDefaults),
    ("ÖB-03", "S-JSON-011 diğer _config dosyaları bozuksa varsayılan", SecurityTests.OtherConfigFilesFallBackToDefaults),
    ("ÖB-03", "S-JSON-012 bozuk _index.json okuması kontrollü", SecurityTests.CorruptIndexFileReadIsControlled),
    ("ÖB-10", "S-JSON-013 dosya adı / Id uyuşmazlığı başka kaydı ezmez", SecurityTests.IdMismatchDoesNotOverwrite),
    ("ÖB-10", "S-JSON-014 yinelenen Id algılanır", SecurityTests.DuplicateIdsAreDetected),
    ("ÖB-10", "S-JSON-015 sonraki kusur numarası taşmaz", SecurityTests.NextIdDoesNotOverflow),
    ("ÖB-10", "S-JSON-016 sıfır / negatif Id reddedilir", SecurityTests.NonPositiveIdsRejectedOnLoad),
    ("ÖB-10", "S-JSON-017 kusur dışı .json yok sayılır", SecurityTests.NonDefectJsonFilesAreIgnored),
    ("ÖB-03", "S-JSON-020b dosyada NaN koordinat VM'de yakalanır", SecurityTests.NanCoordinateInFileHandledByViewModel),
    ("ÖB-06", "S-PIN-004 kilitlenme yeniden açılışta sürer", SecurityTests.LockoutSurvivesNewLoginViewModel),
    ("ÖB-06", "S-PIN-005 tek tek hatalı denemeler denetlenir", SecurityTests.FailedLoginIsAudited),
    ("ÖB-07", "S-PIN-007 admin.json silinince ilk kurulum sessizce açılmaz", SecurityTests.AdminJsonDeletionDoesNotSilentlyResetPin),
    ("ÖB-08", "S-PIN-008 PBKDF2 tur sayısına üst sınır", SecurityTests.HugeIterationCountDoesNotFreezeVerify),
    ("ÖB-08", "S-PIN-009 bozuk Base64 admin.json login'i düşürmez", SecurityTests.BadBase64AdminJsonHandled),
    ("ÖB-08", "S-PIN-010 bozuk admin.json login'i düşürmez", SecurityTests.CorruptAdminJsonHandled),
    ("YENİ", "S-PIN-011b PIN yalnızca ASCII rakam", SecurityTests.PinRejectsNonAsciiDigits),
    ("ÖB-05", "S-AUD-002 sondan satır silinmesi yakalanır", SecurityTests.AuditTailDeletionIsDetected),
    ("ÖB-05", "S-AUD-003 denetim zinciri anahtarlı", SecurityTests.AuditChainIsKeyed),
    ("ÖB-05", "S-AUD-004 kayıt içeriği denetime bağlı", SecurityTests.RecordContentIsBoundToAudit),
    ("ÖB-04", "S-AUD-006 bozuk son satır işlemi bozmaz", SecurityTests.CorruptAuditTailDoesNotBreakOperations),
    ("ÖB-05", "S-AUD-008 _audit.log silinmesi yakalanır", SecurityTests.AuditFileDeletionIsDetected),
    ("YENİ", "S-AUD-011b admin kök değişimi denetlenir", SecurityTests.AdminRootChangeIsAudited),
    ("ÖB-12", "S-LOCK-003 ileri tarihli kilit sonsuza kadar engellemez", SecurityTests.FutureDatedLockIsNotAnEndlessTimeout),
    ("ÖB-12", "S-LOCK-004 admin ileri tarihli kilidi görür", SecurityTests.AdminListsFutureDatedLock),
    ("ÖB-11", "S-PHOTO-004 arka plan aracı boyut taşması", SecurityTests.BackgroundMaskRejectsOverflowingDimensions),
    ("ÖB-14", "S-PHOTO-005 içe aktarmada EXIF temizlenir", SecurityTests.ImportStripsExif),
    ("YENİ", "S-LAUNCH-002 surum.txt = '.' reddedilir", SecurityTests.ProgramLayoutRejectsDotVersion),
    ("YENİ", "S-LAUNCH-003 yalnızca sayısal sürüm biçimi", SecurityTests.ProgramLayoutAcceptsOnlyNumericVersions),
    ("YENİ", "S-LAUNCH-006 göreli rootPath reddedilir / belgelenir", SecurityTests.RootPathExpansionAndRelativeHandling),
};

int failed = 0;
foreach (var (name, body) in tests)
{
    try
    {
        body();
        Console.WriteLine($"  OK    {name}");
    }
    catch (SecurityTests.SkipException skip)
    {
        Console.WriteLine($"  ATLANDI  {name}\n        {skip.Message}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"  FAIL  {name}\n        {ex.Message}");
    }
}

// Açık bulgu listesi: başarısızlık çıkış kodunu etkilemez.
Console.WriteLine();
int open = 0;
foreach (var (id, name, body) in openFindings)
{
    try
    {
        body();
        Console.WriteLine($"  DÜZELDİ  [{id}] {name}  (normal listeye taşıyın)");
    }
    catch (SecurityTests.SkipException skip)
    {
        Console.WriteLine($"  ATLANDI  [{id}] {name}\n        {skip.Message}");
    }
    catch (Exception ex)
    {
        open++;
        Console.WriteLine($"  AÇIK BULGU  [{id}] {name}\n        {ex.Message}");
    }
}
Console.WriteLine($"Açık bulgu: {open}/{openFindings.Count}");

Console.WriteLine();
Console.WriteLine(failed == 0 ? $"Tümü geçti ({tests.Count}/{tests.Count})." : $"{failed} test başarısız.");
return failed == 0 ? 0 : 1;

// ───────────────────────── Yardımcılar ─────────────────────────

static void Expect(bool condition, string message)
{
    if (!condition)
        throw new Exception(message);
}

static Defect NewDefect(int id = 1, string type = "Dent") => new() { Id = id, DefectType = type };

static string TempRoot()
{
    var root = Path.Combine(Path.GetTempPath(), "MuayeneKayit-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    return root;
}

// ───────────────────────── Yol ─────────────────────────

static void SafePathAcceptsValid()
{
    Expect(SafePath.ValidateSegment("PD170", "x") == "PD170", "PD170 kabul edilmeli");
    Expect(SafePath.ValidateSegment("SN-001_A", "x") == "SN-001_A", "SN-001_A kabul edilmeli");
    Expect(SafePath.ValidateSegment("  OP 40 ", "x") == "OP 40", "Baştaki/sondaki boşluk kırpılmalı");
}

static void SafePathRejectsTraversal()
{
    foreach (var bad in new[] { "..", "a..b", @"..\x", "a/b", @"a\b", "x.", "", "   ", "C:" })
    {
        var rejected = false;
        try { SafePath.ValidateSegment(bad, "x"); }
        catch (ArgumentException) { rejected = true; }
        Expect(rejected, $"'{bad}' reddedilmeli");
    }
}

// ───────────────────────── Durum makinesi ─────────────────────────

static void StateReworkReinspectAccept()
{
    var d = NewDefect();
    DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME");
    Expect(DefectStateMachine.GetState(d) == DefectState.AwaitingReinspect, "Rework sonrası muayene bekleme durumunda olmalı");


    DefectStateMachine.Apply(d, Decision.AfterReworkConforms, DecisionSource.Engineer, "M. Kaya", reference: "KAB-1");
    Expect(DefectStateMachine.GetState(d) == DefectState.Closed, "Rework sonrası uygun kapatmalı");
    Expect(d.Dispositions[^1].Snapshot is not null, "Rework sonrası karar ölçüm anlık görüntüsü almalı");
}

static void StateVoidRules()
{
    var d = NewDefect();
    var (engineerOk, _) = DefectStateMachine.Check(d, Decision.Void, DecisionSource.Engineer);
    Expect(!engineerOk, "Mühendis void edememeli");

    DefectStateMachine.Apply(d, Decision.Void, DecisionSource.Operator, "A. Yılmaz", note: "Yanlış giriş");
    Expect(DefectStateMachine.GetState(d) == DefectState.Voided, "Void sonrası Voided olmalı");

    var d2 = NewDefect(2);
    DefectStateMachine.Apply(d2, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME");
    var (reworkOk, _) = DefectStateMachine.Check(d2, Decision.Void, DecisionSource.Operator);
    Expect(!reworkOk, "Rework bekleme durumunda void yapılamamalı");
}

static void StateMrbRejectedScraps()
{
    var d = NewDefect();
    DefectStateMachine.Apply(d, Decision.SubmitMrb, DecisionSource.Engineer, "M. Kaya");
    Expect(DefectStateMachine.GetState(d) == DefectState.MrbPending, "SubmitMrb sonrası MrbPending olmalı");

    DefectStateMachine.Apply(d, Decision.MrbRejected, DecisionSource.Mrb, "MRB", reference: "MRB-1");
    Expect(DefectStateMachine.GetState(d) == DefectState.Closed, "MRB reddi kapatmalı");
    Expect(d.Dispositions[^1].Decision == Decision.Scrap, "MRB reddi otomatik SCRAP yazmalı");
    Expect(d.Dispositions[^1].Source == DecisionSource.System, "Otomatik hurda System kaynaklı olmalı");
}

static void StateVendorIsOpen()
{
    var d = NewDefect();
    DefectStateMachine.Apply(d, Decision.ReturnToVendor, DecisionSource.Engineer, "M. Kaya");
    Expect(DefectStateMachine.GetState(d) == DefectState.WaitingVendor, "RTV sonrası WaitingVendor olmalı");
    Expect(DefectStateMachine.IsOpen(DefectStateMachine.GetState(d)), "Vendor'daki kusur açık sayılmalı");

    DefectStateMachine.Apply(d, Decision.Reinspect, DecisionSource.Engineer, "M. Kaya");
    Expect(DefectStateMachine.GetState(d) == DefectState.AwaitingReinspect, "Vendor dönüşü reinspect'e gitmeli");
}

static void StateSourceRules()
{
    var d = NewDefect();
    var (mrbOk, _) = DefectStateMachine.Check(d, Decision.MrbAccepted, DecisionSource.Engineer);
    Expect(!mrbOk, "Mühendis MRB kararı veremez");

    var (ctpOk, _) = DefectStateMachine.Check(d, Decision.Reinspect, DecisionSource.Ctp);
    Expect(ctpOk, "CTP, Open durumundaki kusura reinspect verebilmeli");

    var (operatorOk, _) = DefectStateMachine.Check(d, Decision.Reinspect, DecisionSource.Operator);
    Expect(!operatorOk, "Operatör reinspect kararı veremez");
}

// ───────────────────────── Geçit ─────────────────────────

static void GateBlocksOpen()
{
    var d = NewDefect();
    DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME");
    Expect(!SessionGate.CanComplete(new[] { d }), "Açık kusur muayeneyi kapatmamalı");
}

static void GateSupersedeResolves()
{
    var parent = NewDefect(1);
    DefectStateMachine.Apply(parent, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME");


    var child = NewDefect(5, "Scratch");
    DefectStateMachine.Apply(parent, Decision.AfterReworkNewDefect, DecisionSource.Engineer, "M. Kaya", newDefectIds: new[] { child.Id });
    child.SupersedesId = parent.Id;
    Expect(DefectStateMachine.GetState(parent) == DefectState.Superseded, "Parent Superseded olmalı");
    Expect(!SessionGate.CanComplete(new[] { parent, child }), "Child açıkken geçit kapalı olmalı");

    DefectStateMachine.Apply(child, Decision.Accept, DecisionSource.Engineer, "M. Kaya", reference: "KAB-3");
    Expect(SessionGate.CanComplete(new[] { parent, child }), "Child kapanınca parent de çözülmüş sayılmalı");

    // MRB akışı ayrıca doğrulanır: önce SubmitMrb, sonra MrbAccepted.
    var mrbChild = NewDefect(6, "Nick");
    DefectStateMachine.Apply(mrbChild, Decision.SubmitMrb, DecisionSource.Engineer, "M. Kaya");
    DefectStateMachine.Apply(mrbChild, Decision.MrbAccepted, DecisionSource.Mrb, "MRB", reference: "MRB-2");
    Expect(DefectStateMachine.GetState(mrbChild) == DefectState.Closed, "MRB kabulü kapatmalı");
}

// ───────────────────────── Cümle ─────────────────────────

static void SentenceFormat()
{
    var d = NewDefect(3, "Dent");
    d.Depth = 0.12;
    d.Width = 2.4;
    d.SpecName = "SPEC-DNT-014";
    d.AcceptanceReference = "KAB-2026-0042";
    DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME");

    var text = DefectSentence.Describe(d);
    Expect(text.StartsWith("Defect 3: There is a Dent on the part: D 0.12 mm, W 2.4 mm."),
        $"Başlık beklenenden farklı: {text}");
    Expect(text.Contains("Disposition: Rework / Reinspect."), "Disposition satırı eksik");
    Expect(text.Contains("Acceptance: KAB-2026-0042."), "Kabul numarası eksik");
}

// ───────────────────────── Depo (geçici klasörde) ─────────────────────────

static Session SampleSession(string serial = "SN001") => new()
{
    PartNumber = "PD170",
    OperationNumber = "OP40",
    SerialNumber = serial,
    Inspector = "A. Yılmaz",
    Project = "PD170",
};

static void StoreIdsNeverReused()
{
    var root = TempRoot();
    try
    {
        var store = new SessionStore(root);
        var (folder, _) = store.OpenOrCreate(SampleSession());

        var first = store.AddDefect(folder, "Dent");
        var second = store.AddDefect(folder, "Nick");
        Expect(first.Id == 1 && second.Id == 2, "İlk iki numara 1 ve 2 olmalı");

        store.UpdateDefect(folder, second.Id, d =>
            DefectStateMachine.Apply(d, Decision.Void, DecisionSource.Operator, "A. Yılmaz", note: "Yanlış"));

        var third = store.AddDefect(folder, "Scratch");
        Expect(third.Id == 3, $"Void edilen numara yeniden kullanılmamalı, gelen: {third.Id}");
    }
    finally { Directory.Delete(root, recursive: true); }
}

static void StoreOpenOrCreateKeepsExisting()
{
    var root = TempRoot();
    try
    {
        var store = new SessionStore(root);
        var (folder, _) = store.OpenOrCreate(SampleSession());
        store.AddDefect(folder, "Dent");

        var (_, again) = store.OpenOrCreate(SampleSession());
        Expect(again.Inspector == "A. Yılmaz", "Mevcut oturum döndürülmeli");
        Expect(store.LoadDefects(folder).Count == 1, "Mevcut kusurlar korunmalı");
    }
    finally { Directory.Delete(root, recursive: true); }
}

static void IndexRebuildsRows()
{
    var root = TempRoot();
    try
    {
        var store = new SessionStore(root);
        var (folder, _) = store.OpenOrCreate(SampleSession("SN777"));
        store.AddDefect(folder, "Dent", d => DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME"));

        var rows = IndexBuilder.Rebuild(store);
        Expect(rows.Count == 1, $"Bir satır beklenirdi, gelen: {rows.Count}");
        Expect(rows[0].OpenDefects == 1, "Açık kusur sayısı 1 olmalı");

        var read = IndexBuilder.Read(store);
        Expect(IndexBuilder.ForSerial(read, "sn777").Count() == 1, "Seri arama büyük/küçük harf duyarsız olmalı");
    }
    finally { Directory.Delete(root, recursive: true); }
}

static void AuditChainDetectsTamper()
{
    var root = TempRoot();
    try
    {
        var store = new SessionStore(root);
        AuditLog.Append(store, "A. Yılmaz", "defect.add", "#1 Dent");
        AuditLog.Append(store, "M. Kaya", "disposition", "#1 Rework");
        AuditLog.Append(store, "M. Kaya", "disposition", "#1 Reinspect");

        Expect(AuditLog.Verify(store).Ok, "Sağlam zincir doğrulanmalı");

        var path = Path.Combine(root, AuditLog.FileName);
        var lines = File.ReadAllLines(path);
        lines[1] = lines[1].Replace("Rework", "Accept");
        File.WriteAllLines(path, lines);

        var (ok, badLine) = AuditLog.Verify(store);
        Expect(!ok && badLine == 2, $"Kurcalanmış satır 2'de yakalanmalı, gelen: {badLine}");
    }
    finally { Directory.Delete(root, recursive: true); }
}

static void ValidatorRules()
{
    var ok = new Defect { DefectType = "Dent", Depth = 0.12, Width = 2.4, AngleDeg = 45, Color = "gri" };
    Expect(DefectValidator.Validate(ok).Count == 0, "Geçerli ölçümler hata üretmemeli");

    var bad = new Defect { DefectType = "Dent", Depth = -1, AngleDeg = 400 };
    var errors = DefectValidator.Validate(bad);
    Expect(errors.Any(e => e.Contains("Derinlik")), "Negatif derinlik reddedilmeli");
    Expect(errors.Any(e => e.Contains("Açı")), "360 üstü açı reddedilmeli");

    var empty = new Defect { DefectType = "  " };
    Expect(DefectValidator.Validate(empty).Any(e => e.Contains("Hata tipi")), "Boş hata tipi reddedilmeli");
}

static void PhotoImportRules()
{
    var root = TempRoot();
    try
    {
        var store = new SessionStore(root);
        var (folder, _) = store.OpenOrCreate(SampleSession("SN900"));

        // Geçerli JPEG başlığı (FF D8 FF) ile sahte bir içerik üret.
        var jpg = Path.Combine(root, "ornek.jpg");
        File.WriteAllBytes(jpg, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46 });

        var name = PhotoImporter.Import(folder, 1, jpg);
        Expect(name == "1_photo_01.jpg", $"Dosya adı beklenenden farklı: {name}");
        Expect(File.Exists(Path.Combine(folder, "defects", name)), "Fotoğraf kusur klasörüne kopyalanmalı");

        var name2 = PhotoImporter.Import(folder, 1, jpg);
        Expect(name2 == "1_photo_02.jpg", "İkinci fotoğraf sıradaki numarayı almalı");

        // Uzantısı .jpg ama içeriği metin: reddedilmeli.
        var fake = Path.Combine(root, "sahte.jpg");
        File.WriteAllText(fake, "bu bir resim degil");
        var rejected = false;
        try { PhotoImporter.Import(folder, 1, fake); }
        catch (InvalidDataException) { rejected = true; }
        Expect(rejected, "İçeriği görüntü olmayan dosya reddedilmeli");

        // Desteklenmeyen uzantı.
        var exe = Path.Combine(root, "calistir.exe");
        File.WriteAllBytes(exe, new byte[] { 0x4D, 0x5A });
        var rejectedExt = false;
        try { PhotoImporter.Import(folder, 1, exe); }
        catch (InvalidDataException) { rejectedExt = true; }
        Expect(rejectedExt, ".exe dosyası reddedilmeli");
    }
    finally { Directory.Delete(root, recursive: true); }
}


static void SameDefectLoops()
{
    var d = NewDefect();
    DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME");
    d.Depth = 0.20;
    DefectStateMachine.Apply(d, Decision.AfterReworkSameDefect, DecisionSource.Engineer, "M. Kaya", note: "Ölçüm değişti, hâlâ var");
    Expect(DefectStateMachine.GetState(d) == DefectState.Open, "Aynı kusur devam ederse Open'a dönmeli");
    Expect(d.Dispositions[^1].Snapshot?.Depth == 0.20, "Ölçüm değişikliği anlık görüntüde kalmalı");
    DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME");
    Expect(DefectStateMachine.GetState(d) == DefectState.AwaitingReinspect, "Tekrar rework yapılabilmeli");
}

static void ReopenRules()
{
    var d = NewDefect();
    DefectStateMachine.Apply(d, Decision.Accept, DecisionSource.Engineer, "M. Kaya");
    var threw = false;
    try { DefectStateMachine.Apply(d, Decision.Reopen, DecisionSource.Engineer, "M. Kaya", note: " "); }
    catch (InvalidOperationException) { threw = true; }
    Expect(threw, "Notsuz yeniden açma reddedilmeli");

    DefectStateMachine.Apply(d, Decision.Reopen, DecisionSource.Engineer, "M. Kaya", note: "Ölçüm yanlış girilmişti");
    Expect(DefectStateMachine.GetState(d) == DefectState.Open, "Yeniden açılan kusur Open olmalı");
    Expect(d.Dispositions.Count == 2, "Geçmiş silinmeden yeni kayıt eklenmeli");
    Expect(d.Dispositions[0].Decision == Decision.Accept, "Eski kabul kaydı korunmalı");
}

static void ScrapIsFinal()
{
    var d = NewDefect();
    DefectStateMachine.Apply(d, Decision.Scrap, DecisionSource.Engineer, "M. Kaya");
    var (ok, _) = DefectStateMachine.Check(d, Decision.Reopen, DecisionSource.Engineer);
    Expect(!ok, "Hurda kararı yeniden açılamamalı");
}

static void ContinueAsChildLinks()
{
    var root = TempRoot();
    try
    {
        var store = new SessionStore(root);
        var (folder, _) = store.OpenOrCreate(SampleSession("SN555"));
        var parent = store.AddDefect(folder, "Dent");
        store.UpdateDefect(folder, parent.Id, d => DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME"));


        var child = store.ContinueAsChild(folder, parent.Id, "Scratch", DecisionSource.Ctp, "CTP", "Yeni çizik bulundu", null);
        Expect(child.Id == 2, $"Child yeni numara almalı, gelen: {child.Id}");

        var reloaded = store.LoadDefects(folder);
        var p = reloaded.First(d => d.Id == parent.Id);
        var c = reloaded.First(d => d.Id == child.Id);
        Expect(p.Dispositions[^1].NewDefectIds is { Count: 1 } ids && ids[0] == child.Id && p.SupersededById is null, "Parent child'a karar üzerinden bağlanmalı (eski tek bağ yazılmaz)");
        Expect(c.SupersedesId == parent.Id, "Child parent'a bağlanmalı");
        Expect(p.Dispositions[^1].Decision == Decision.AfterReworkNewDefect, "Parent devam kararını almalı");
        Expect(!SessionGate.CanComplete(reloaded), "Açık child varken muayene kapanmamalı");
    }
    finally { Directory.Delete(root, recursive: true); }
}

static void UndoRestoresPreviousState()
{
    var d = NewDefect();
    DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME");
    Expect(DefectStateMachine.GetState(d) == DefectState.AwaitingReinspect, "Rework sonrası bekleme");

    var noNote = false;
    try { DefectStateMachine.Undo(d, "M. Kaya", " "); }
    catch (InvalidOperationException) { noNote = true; }
    Expect(noNote, "Gerekçesiz geri alma reddedilmeli");

    var undone = DefectStateMachine.Undo(d, "M. Kaya", "Yanlış karar seçildi");
    Expect(undone.Decision == Decision.Rework, "Geri alınan karar Rework olmalı");
    Expect(DefectStateMachine.GetState(d) == DefectState.Open, "Kusur açık duruma dönmeli");
    Expect(d.Dispositions.Count == 2, "Kayıt silinmemeli, Undo satırı eklenmeli");
    Expect(DefectStateMachine.Check(d, Decision.Accept, DecisionSource.Engineer).Ok, "Yeni karar verilebilmeli");

    var none = false;
    try { DefectStateMachine.Undo(d, "M. Kaya", "tekrar"); }
    catch (InvalidOperationException) { none = true; }
    Expect(none, "Geri alınacak karar kalmamışsa hata vermeli");
}

static void UndoMrbRejectedRemovesAutoScrap()
{
    var d = NewDefect();
    DefectStateMachine.Apply(d, Decision.SubmitMrb, DecisionSource.Engineer, "M. Kaya");
    DefectStateMachine.Apply(d, Decision.MrbRejected, DecisionSource.Mrb, "MRB");
    Expect(DefectStateMachine.GetState(d) == DefectState.Closed, "MRB reddi kapatır");

    DefectStateMachine.Undo(d, "MRB", "Yanlış tıklandı");
    Expect(DefectStateMachine.GetState(d) == DefectState.MrbPending, "Geri alınca MRB beklemeye dönmeli (otomatik hurda da geri alınır)");
    Expect(DefectStateMachine.UndoneSeqs(d).SetEquals(new[] { 2, 3 }), "MRB reddi ve otomatik hurda birlikte geri alınmalı");

    var html = ReportBuilder.Build(SampleSession("SN-U"), new[] { d }, version: 1);
    Expect(!html.Contains("MRB rejected") && !html.Contains("Geri alındı") && html.Contains("Submit MRB"),
        "Raporda geri alınan kararlar görünmemeli, geçerli karar görünmeli");
    Expect(!html.Contains("Kusur özeti") && html.Contains("Sayfa / Page 1 / 1"), "Özet sayfası olmamalı, sayfa sayısı kusur sayısı kadar");
}

static void UndoChildLinkVoidsChild()
{
    var root = TempRoot();
    try
    {
        var store = new SessionStore(root);
        var (folder, _) = store.OpenOrCreate(SampleSession("SN-UC"));
        var parent = store.AddDefect(folder, "Undercut");
        store.UpdateDefect(folder, parent.Id, d => DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME"));
        var child = store.ContinueAsChild(folder, parent.Id, "Dent", DecisionSource.Engineer, "M. Kaya", null, null);

        // Child'a karar verilmişse önce o geri alınmalı.
        store.UpdateDefect(folder, child.Id, d => DefectStateMachine.Apply(d, Decision.Accept, DecisionSource.Engineer, "M. Kaya"));
        var blocked = false;
        try { store.UndoLastDecision(folder, parent.Id, "M. Kaya", "yanlış"); }
        catch (InvalidOperationException) { blocked = true; }
        Expect(blocked, "Child'ın kararı varken parent geri alınmamalı");

        store.UndoLastDecision(folder, child.Id, "M. Kaya", "yanlış kabul");
        var (undone, voided) = store.UndoLastDecision(folder, parent.Id, "M. Kaya", "Dent yanlış bağlandı");
        Expect(undone.Decision == Decision.AfterReworkNewDefect && voided.SequenceEqual(new[] { child.Id }), "Devir geri alınmalı, child void edilmeli");

        var all = store.LoadDefects(folder);
        var p = all.First(x => x.Id == parent.Id);
        var c = all.First(x => x.Id == child.Id);
        Expect(p.SupersededById is null, "Parent bağı çözülmeli");
        Expect(DefectStateMachine.GetState(p) == DefectState.AwaitingReinspect, "Parent rework sonrası muayene beklemeye dönmeli");
        Expect(DefectStateMachine.GetState(c) == DefectState.Voided, "Child void olmalı (silinmez)");

        var html = ReportBuilder.Build(SampleSession("SN-UC"), all, 1);
        Expect(!html.Contains("Continues as Defect"), "Rapor eski bağı göstermemeli");
    }
    finally { Directory.Delete(root, recursive: true); }
}

static void HighMetalSentenceAndReport()
{
    var d = NewDefect(4, "Dent");
    d.Depth = 12;
    Expect(DefectSentence.Describe(d).Contains("There is a Dent on the part: D 12 mm."), "High metal yokken cümle değişmemeli");
    d.HighMetal = true;
    Expect(DefectSentence.Describe(d).Contains("There is a Dent with high metal on the part: D 12 mm."), "High metal cümlede yazılmalı");

    var html = ReportBuilder.Build(SampleSession("SN-HM"), new[] { d }, 1);
    Expect(html.Contains("Dent with high metal"), "Rapor başlığında high metal görünmeli");

    // Eski kayıt: highMetal alanı yok → false.
    var old = System.Text.Json.JsonSerializer.Deserialize<Defect>(
        "{\"id\":1,\"defectType\":\"Dent\",\"dispositions\":[{\"seq\":1,\"decision\":\"Rework\",\"snapshot\":{\"depth\":1}}]}",
        SessionStore.JsonOptions)!;
    Expect(!old.HighMetal && old.Dispositions[0].Snapshot!.HighMetal == false, "Eski kayıtta high metal false okunmalı");
}

static void HighMetalEditLogged()
{
    var d = NewDefect();
    d.Depth = 2;
    var same = MeasurementEditor.Snapshot(d);
    Expect(!MeasurementEditor.Apply(d, same, "A"), "Değişiklik yoksa kayıt olmamalı");

    Expect(MeasurementEditor.Apply(d, same with { HighMetal = true }, "A"), "Yalnızca high metal değişikliği kaydedilmeli");
    Expect(d.HighMetal && d.MeasurementEdits.Count == 1, "Tek düzeltme kaydı");
    Expect(!d.MeasurementEdits[0].Before.HighMetal && d.MeasurementEdits[0].After.HighMetal, "Önce yok, sonra var");
}

static void Scenario5_HighMetalOnlyRework()
{
    var d = NewDefect(4, "Dent");
    d.Depth = 12;
    d.HighMetal = true;

    DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "OP60",
        reworkScope: ReworkScope.HighMetalOnly);
    Trace("1. Rework (yalnızca high metal)", d);
    Expect(d.Dispositions[0].Snapshot!.HighMetal, "Rework anı görüntüsü high metal içermeli");
    Expect(DefectSentence.Describe(d).Contains("Rework / Reinspect (high metal only)"), "Kapsam karar etiketinde görünmeli");

    // Rework sonrası: high metal gitti, derinlik aynı.
    MeasurementEditor.Apply(d, MeasurementEditor.Snapshot(d) with { HighMetal = false }, "CTP");
    DefectStateMachine.Apply(d, Decision.AfterReworkSameDefect, DecisionSource.Engineer, "CTP",
        note: "High metal giderildi; ana kusur devam ediyor.");
    Trace("2. High metal giderildi, Dent devam", d);

    Expect(DefectStateMachine.GetState(d) == DefectState.Open, "Ana kusur açık kalmalı");
    Expect(DefectSentence.Describe(d).Contains("There is a Dent on the part: D 12 mm."), "Cümlede high metal kalmamalı");
    var lines = MeasurementCompare.Lines(d.Dispositions[0].Snapshot, d);
    Expect(lines.Contains("High metal: var → giderildi") && lines.Any(l => l.StartsWith("Derinlik: 12 → 12")),
        "Değişim: high metal giderildi, derinlik aynı");

    Expect(DefectStateMachine.Check(d, Decision.SubmitMrb, DecisionSource.Engineer).Ok, "Kalan kusur MRB'ye gönderilebilmeli");
    DefectStateMachine.Apply(d, Decision.SubmitMrb, DecisionSource.Engineer, "M. Kaya");
    Trace("3. Ana kusur MRB'de", d);

    var bad = false;
    try { DefectStateMachine.Apply(NewDefect(), Decision.Accept, DecisionSource.Engineer, "x", reworkScope: ReworkScope.HighMetalOnly); }
    catch (InvalidOperationException) { bad = true; }
    Expect(bad, "Kapsam yalnızca rework kararında verilebilir");

    var html = ReportBuilder.Build(SampleSession("SN-HM5"), new[] { d }, 1);
    Expect(html.Contains("Rework / Reinspect (high metal only)") && html.Contains("High metal"), "Rapor geçmişinde kapsam ve high metal");
}

static void CameraFrameSquare()
{
    var a = Annotation.CenterSquare(1920, 1080, 9);
    var wPx = (a.X2 - a.X1) * 1920;
    var hPx = (a.Y2 - a.Y1) * 1080;
    Expect(Math.Abs(wPx - 216) < 0.01 && Math.Abs(hPx - 216) < 0.01, $"Varsayılan kare 216 px olmalı ({wPx}x{hPx})");
    var big = Annotation.CenterSquare(1920, 1080, 9, 0.5);
    Expect(Math.Abs((big.Y2 - big.Y1) * 1080 - 540) < 0.01, "Boyut oranı uygulanmalı (540 px)");
    Expect(Math.Abs((a.X1 + a.X2) / 2 - 0.5) < 1e-9 && Math.Abs((a.Y1 + a.Y2) / 2 - 0.5) < 1e-9, "Ortalanmış olmalı");
    Expect(a.Kind == AnnotationKind.Box && a.DefectId == 9, "Kutu ve kusur no");

    var markup = new PhotoMarkup { ImageWidth = 1920, ImageHeight = 1080, Shapes = { a } };
    Expect(PhotoMarkupValidator.Validate(markup).Count == 0, "Çerçeve geçerli işaret olmalı");
}

static void ReportPhotoPerPage()
{
    var d = NewDefect(6, "Deformation");
    d.Photos.AddRange(new[] { "6_photo_01.jpg", "6_photo_02.jpg", "6_photo_03.jpg" });
    DefectStateMachine.Apply(d, Decision.SubmitMrb, DecisionSource.Engineer, "M. Kaya");
    var single = NewDefect(7, "Nick");

    var html = ReportBuilder.Build(SampleSession("SN-P"), new[] { d, single }, 1);
    Expect(html.Contains(">#6.1<") && html.Contains(">#6.2<") && html.Contains(">#6.3<"), "Her fotoğraf 6.1, 6.2, 6.3 olarak etiketlenmeli");
    Expect(html.Contains(">#7<") && !html.Contains("#7.1"), "Tek / fotoğrafsız kusur alt numara almamalı");
    Expect(html.Contains("Sayfa / Page 4 / 4"), "3 fotoğraf + 1 kusur = 4 sayfa");
    Expect(System.Text.RegularExpressions.Regex.Matches(html, "6_photo_0").Count >= 3, "Her fotoğraf kendi sayfasında");
    Expect(System.Text.RegularExpressions.Regex.Matches(html, "Submit MRB").Count == 1, "Karar geçmişi yalnızca ilk sayfada");
}

static void DecisionTextRules()
{
    var root = TempRoot();
    try
    {
        Expect(SharedConfig.LoadDecisionTexts(root)[Decision.Accept] == SharedConfig.DefaultDecisionTexts[Decision.Accept], "Dosya yoksa varsayılan");

        SharedConfig.SaveDecisionTexts(root, new Dictionary<Decision, string> { [Decision.Rework] = "  Parça rework hattına gider.  " });
        var loaded = SharedConfig.LoadDecisionTexts(root);
        Expect(loaded[Decision.Rework] == "Parça rework hattına gider.", "Düzenlenen metin okunmalı (kırpılmış)");
        Expect(loaded[Decision.Scrap] == SharedConfig.DefaultDecisionTexts[Decision.Scrap], "Düzenlenmeyen karar varsayılan kalmalı");

        var rejected = false;
        try { SharedConfig.SaveDecisionTexts(root, new Dictionary<Decision, string> { [Decision.Accept] = new string('x', 700) }); }
        catch (ArgumentException) { rejected = true; }
        Expect(rejected, "Çok uzun metin reddedilmeli");

        rejected = false;
        try { SharedConfig.SaveDecisionTexts(root, new Dictionary<Decision, string> { [Decision.Undo] = "x" }); }
        catch (ArgumentException) { rejected = true; }
        Expect(rejected, "Listede olmayan karar için metin reddedilmeli");
    }
    finally { Directory.Delete(root, true); }
}

static void ReportEscapesAndHidesVoid()
{
    var session = SampleSession("SN<script>");
    var visible = NewDefect(1, "Dent<img onerror=x>");
    visible.Notes = "<b>not</b>";
    var voided = NewDefect(2, "Nick");
    DefectStateMachine.Apply(voided, Decision.Void, DecisionSource.Operator, "A. Yılmaz", note: "Yanlış");

    var html = ReportBuilder.Build(session, new[] { visible, voided }, version: 1);
    Expect(!html.Contains("<script>"), "Seri numarası HTML olarak çıkmamalı");
    Expect(!html.Contains("<img onerror"), "Hata tipi HTML olarak çıkmamalı");
    Expect(html.Contains("&lt;b&gt;not&lt;/b&gt;"), "Notlar kaçılmalı");
    Expect(!html.Contains("Defect 2") && !html.Contains("#2 "), "Void kayıt rapora girmemeli");
    Expect(html.Contains("@page { size: A4 landscape"), "Rapor yatay A4 olmalı");
}

// ───────────────────────── İşaretleme, arka plan, tarama, admin ─────────────────────────

static void MarkupValidation()
{
    var ok = new PhotoMarkup { ImageWidth = 100, ImageHeight = 80 };
    ok.Shapes.Add(new Annotation { Kind = AnnotationKind.Box, X1 = 0.1, Y1 = 0.1, X2 = 0.5, Y2 = 0.6, DefectId = 1 });
    Expect(PhotoMarkupValidator.Validate(ok).Count == 0, "Geçerli işaret kabul edilmeli");

    var outside = new PhotoMarkup { ImageWidth = 100, ImageHeight = 80 };
    outside.Shapes.Add(new Annotation { Kind = AnnotationKind.Arrow, X1 = -0.1, Y1 = 0, X2 = 1.4, Y2 = 1, DefectId = 1 });
    Expect(PhotoMarkupValidator.Validate(outside).Count > 0, "Görüntü dışı koordinat reddedilmeli");

    var badFile = new PhotoMarkup { ImageWidth = 100, ImageHeight = 80, CleanedFile = "..\\..\\session.json" };
    Expect(PhotoMarkupValidator.Validate(badFile).Count > 0, "Temiz kopya adı yol içeremez");
}

static void ReportDrawsMarkup()
{
    var d = NewDefect(3, "Dent");
    d.Photos.Add("3_photo_01.jpg");
    var markup = new PhotoMarkup { ImageWidth = 1920, ImageHeight = 1080, CleanedFile = "3_photo_01_clean.jpg" };
    markup.Shapes.Add(new Annotation { Kind = AnnotationKind.Box, X1 = 0.2, Y1 = 0.2, X2 = 0.4, Y2 = 0.5, DefectId = 3 });
    markup.Shapes.Add(new Annotation { Kind = AnnotationKind.Arrow, X1 = 0.8, Y1 = 0.8, X2 = 0.5, Y2 = 0.5, DefectId = 1 });
    markup.Shapes.Add(new Annotation { Kind = AnnotationKind.Circle, X1 = 0.6, Y1 = 0.1, X2 = 0.7, Y2 = 0.3, DefectId = 3 });
    d.Markups["3_photo_01.jpg"] = markup;

    var html = ReportBuilder.Build(SampleSession("SN-M"), new[] { d }, version: 1);
    Expect(html.Contains("viewBox=\"0 0 1920 1080\""), "SVG görüntü boyutunu kullanmalı");
    Expect(html.Contains("<rect x=\"384\" y=\"216\""), "Kutu piksel koordinatına çevrilmeli");
    Expect(html.Contains("<ellipse") && html.Contains("marker-end"), "Daire ve ok çizilmeli");
    Expect(html.Contains(">#3</text>") && html.Contains(">#1</text>"), "Kusur numarası etiketi yazılmalı");
    Expect(html.Contains("href=\"../defects/3_photo_01_clean.jpg\""), "Arka planı kaldırılmış kopya kullanılmalı");
}

static void BackgroundSeparatesPart()
{
    const int w = 300, h = 200;
    var px = new byte[w * h * 4];
    var rng = new Random(7);
    for (var y = 0; y < h; y++)
    {
        for (var x = 0; x < w; x++)
        {
            var part = x >= 90 && x < 210 && y >= 50 && y < 150;
            var hole = x >= 140 && x < 160 && y >= 90 && y < 110;   // parçanın üstünde koyu leke, delik sayılmamalı
            var n = rng.Next(-12, 13);
            var (r, g, b) = part ? (hole ? (20, 20, 25) : (60, 70, 150)) : (215, 210, 200);
            var p = (y * w + x) * 4;
            px[p] = (byte)Math.Clamp(b + n, 0, 255);
            px[p + 1] = (byte)Math.Clamp(g + n, 0, 255);
            px[p + 2] = (byte)Math.Clamp(r + n, 0, 255);
            px[p + 3] = 255;
        }
    }

    var mask = BackgroundRemover.ComputeMask(px, w, h, new PixelRect(60, 30, 180, 140));
    Expect(mask[100 * w + 150 - 30], "Parça ortası ön plan olmalı");
    Expect(mask[100 * w + 150], "Parçadaki koyu leke ön planda kalmalı");
    Expect(!mask[35 * w + 65], "Dikdörtgen içindeki zemin arka plan olmalı");
    Expect(!mask[5 * w + 5], "Dikdörtgen dışı arka plan olmalı");

    var inside = 0;
    for (var y = 50; y < 150; y++) for (var x = 90; x < 210; x++) if (mask[y * w + x]) inside++;
    Expect(inside > 0.97 * 120 * 100, $"Parçanın en az %97'si ön plan olmalı ({inside})");

    var output = BackgroundRemover.Apply(px, mask);
    Expect(output[(5 * w + 5) * 4] == 255 && px[(5 * w + 5) * 4] != 255, "Zemin beyaza boyanmalı, girdi değişmemeli");
}

static void CleanedCopyKeepsOriginal()
{
    var root = TempRoot();
    try
    {
        var store = new SessionStore(root);
        var (folder, _) = store.OpenOrCreate(SampleSession("SN-CL"));
        var jpg = Path.Combine(root, "a.jpg");
        File.WriteAllBytes(jpg, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3 });
        var original = PhotoImporter.Import(folder, 1, jpg);

        var clean = PhotoImporter.WriteCleaned(folder, original, new byte[] { 0xFF, 0xD8, 0xFF, 9, 9 });
        Expect(clean == "1_photo_01_clean.jpg", $"Temiz kopya adı: {clean}");
        Expect(File.ReadAllBytes(Path.Combine(folder, "defects", original)).Length == 7, "Orijinal değişmemeli");
        Expect(PhotoImporter.Import(folder, 1, jpg) == "1_photo_02.jpg", "Temiz kopya numaralamaya sayılmamalı");

        var rejected = false;
        try { PhotoImporter.WriteCleaned(folder, "..\\x.jpg", new byte[] { 0xFF, 0xD8, 0xFF }); }
        catch (ArgumentException) { rejected = true; }
        Expect(rejected, "Geçersiz ad reddedilmeli");
    }
    finally { Directory.Delete(root, true); }
}

static void InstallSettingsFoundInParentFolders()
{
    var root = TempRoot();
    try
    {
        // Ağ düzeni: Program\MuayeneKayit.settings.json, exe Program\1.0.0\Inspector-x64 içinde (iki üst klasör).
        var exeDir = Path.Combine(root, "Program", "1.0.0", "Inspector-x64");
        Directory.CreateDirectory(exeDir);
        Expect(InstallSettings.Find(exeDir) is null, "Dosya yokken null");
        Expect(InstallSettings.LoadRootPath(exeDir).EndsWith("MuayeneKayitDemo"), "Dosya yokken demo klasörü");

        var shared = Path.Combine(root, "Program", InstallSettings.FileName);
        File.WriteAllText(shared, "{ \"rootPath\": \"\\\\\\\\sunucu\\\\Veri\" }");
        Expect(InstallSettings.Find(exeDir) == shared, "Üst klasördeki dosya bulunmalı");
        Expect(InstallSettings.LoadRootPath(exeDir) == @"\\sunucu\Veri", "Kökteki yol okunmalı");

        // Exe'nin yanındaki dosya önceliklidir; kayıt bulunan dosyaya yazılır.
        File.WriteAllText(Path.Combine(exeDir, InstallSettings.FileName), "{ \"rootPath\": \"D:\\\\Yerel\" }");
        Expect(InstallSettings.LoadRootPath(exeDir) == @"D:\Yerel", "Exe yanındaki dosya öncelikli");
        File.Delete(Path.Combine(exeDir, InstallSettings.FileName));
        InstallSettings.SaveRootPath(exeDir, @"\\sunucu\Yeni");
        Expect(InstallSettings.LoadRootPath(exeDir) == @"\\sunucu\Yeni" && !File.Exists(Path.Combine(exeDir, InstallSettings.FileName)),
            "Kayıt kökteki dosyaya yazılmalı");

        // Dosya hiç yokken Admin'den kayıt: surum.txt'nin yanına (program köküne) yazılır, exe klasörüne değil.
        File.Delete(shared);
        File.WriteAllText(Path.Combine(root, "Program", ProgramLayout.VersionFile), "1.0.0");
        var adminDir = Path.Combine(root, "Program", "1.0.0", "Admin-x64");
        Directory.CreateDirectory(adminDir);
        InstallSettings.SaveRootPath(adminDir, @"\\sunucu\Ilk");
        Expect(File.Exists(shared) && !File.Exists(Path.Combine(adminDir, InstallSettings.FileName)), "İlk kayıt program köküne yazılmalı");
        Expect(InstallSettings.LoadRootPath(exeDir) == @"\\sunucu\Ilk", "Inspector aynı yolu görmeli");
    }
    finally { Directory.Delete(root, true); }
}

static void ProgramLayoutResolvesVersionAndArch()
{
    var root = TempRoot();
    try
    {
        Expect(Throws(() => ProgramLayout.Resolve(root, "Inspector", false)), "surum.txt yoksa hata");
        File.WriteAllText(Path.Combine(root, ProgramLayout.VersionFile), "1.0.1\r\n");
        Expect(Throws(() => ProgramLayout.Resolve(root, "Inspector", false)), "Sürüm klasörü yoksa hata");

        string Exe(string app, string arch)
        {
            var dir = Path.Combine(root, "1.0.1", $"{app}-{arch}");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"MuayeneKayit.{app}.exe");
            File.WriteAllText(path, "");
            return path;
        }

        var x64 = Exe("Inspector", "x64");
        Expect(ProgramLayout.Resolve(root, "Inspector", false) == x64, "x64");
        Expect(ProgramLayout.Resolve(root, "Inspector", true) == x64, "ARM64 sürümü yoksa x64");
        var arm = Exe("Inspector", "arm64");
        Expect(ProgramLayout.Resolve(root, "Inspector", true) == arm, "ARM64 bilgisayarda ARM64 sürümü");
        Expect(Throws(() => ProgramLayout.Resolve(root, "Admin", false)), "Admin yoksa hata");

        File.WriteAllText(Path.Combine(root, ProgramLayout.VersionFile), "..\\x");
        Expect(Throws(() => ProgramLayout.Resolve(root, "Inspector", false)), "Geçersiz sürüm adı reddedilmeli");
    }
    finally { Directory.Delete(root, true); }
}

static bool Throws(Action action)
{
    try { action(); return false; }
    catch (FileNotFoundException) { return true; }
}

static void ReportVersionRules()
{
    var root = TempRoot();
    try
    {
        Expect(ReportFiles.LatestVersion(root) == 0, "Rapor yokken sürüm 0");
        Directory.CreateDirectory(Path.Combine(root, "reports"));
        File.WriteAllText(Path.Combine(root, "reports", "Rapor_v1.html"), "x");
        File.WriteAllText(Path.Combine(root, "reports", "Rapor_v2.html"), "x");
        File.WriteAllText(Path.Combine(root, "reports", "Rapor_v2.pdf"), "x");
        Expect(ReportFiles.LatestVersion(root) == 2, "Son sürüm 2 olmalı");

        // Yalnızca PDF'i olan sürüm de sayılır (HTML silinmiş).
        File.WriteAllText(Path.Combine(root, "reports", "Rapor_v3.pdf"), "x");
        Expect(ReportFiles.LatestVersion(root) == 3, "HTML'siz PDF sürümü sayılmalı");

        // Temizlik: PDF'i olan HTML silinir, PDF'i olmayan (v1) yedek olarak kalır.
        Expect(ReportFiles.CleanupHtml(root) == 1, "Yalnızca v2 HTML silinmeli");
        Expect(File.Exists(Path.Combine(root, "reports", "Rapor_v1.html")) && !File.Exists(Path.Combine(root, "reports", "Rapor_v2.html")),
            "v1 HTML kalmalı, v2 HTML silinmeli");
        Expect(ReportFiles.LatestVersion(root) == 3, "Temizlikten sonra sürüm değişmemeli");
    }
    finally { Directory.Delete(root, true); }
}

static void DefectTypeTemplates()
{
    var root = TempRoot();
    try
    {
        Expect(SharedConfig.LoadDefectTypes(root).Count == SharedConfig.Defaults.Count, "Dosya yoksa varsayılanlar");

        var types = new List<DefectTypeTemplate>
        {
            new() { Name = "Çentik", Fields = { MeasureField.Depth }, SpecName = "SPA-12" },
            new() { Name = "Pitting", Fields = { MeasureField.Depth, MeasureField.Radius } },
        };
        SharedConfig.SaveDefectTypes(root, types);
        var loaded = SharedConfig.LoadDefectTypes(root);
        Expect(loaded.Count == 2 && loaded[0].Name == "Çentik" && loaded[0].SpecName == "SPA-12", "Şablon geri okunmalı");
        Expect(loaded[1].Fields.SequenceEqual(new[] { MeasureField.Depth, MeasureField.Radius }), "Alanlar korunmalı");

        var rejected = false;
        try { SharedConfig.SaveDefectTypes(root, new[] { new DefectTypeTemplate { Name = "<script>" } }); }
        catch (ArgumentException) { rejected = true; }
        Expect(rejected, "Geçersiz ad reddedilmeli");

        var dup = SharedConfig.ValidateTypes(new[] { new DefectTypeTemplate { Name = "Dent" }, new DefectTypeTemplate { Name = "dent" } });
        Expect(dup.Count > 0, "Aynı ad iki kez olmamalı");

        File.WriteAllText(Path.Combine(SharedConfig.Folder(root), "defect_types.json"), "{bozuk");
        Expect(SharedConfig.LoadDefectTypes(root).Count == SharedConfig.Defaults.Count, "Bozuk dosyada varsayılanlar");
    }
    finally { Directory.Delete(root, true); }
}

static void AdminPinRules()
{
    var root = TempRoot();
    try
    {
        Expect(!SharedConfig.HasPin(root) && !SharedConfig.VerifyPin(root, "123456"), "PIN yokken giriş olmamalı");

        var rejected = false;
        try { SharedConfig.SetPin(root, "1234"); }
        catch (ArgumentException) { rejected = true; }
        Expect(rejected, "Kısa PIN reddedilmeli");

        SharedConfig.SetPin(root, "482913");
        Expect(SharedConfig.VerifyPin(root, "482913"), "Doğru PIN kabul edilmeli");
        Expect(!SharedConfig.VerifyPin(root, "482914"), "Yanlış PIN reddedilmeli");

        var raw = File.ReadAllText(Path.Combine(SharedConfig.Folder(root), "admin.json"));
        Expect(!raw.Contains("482913"), "PIN düz metin saklanmamalı");
    }
    finally { Directory.Delete(root, true); }
}

static void StaleLockRules()
{
    var root = TempRoot();
    var outside = Path.Combine(Path.GetTempPath(), "mk-outside-" + Guid.NewGuid().ToString("N") + ".lock");
    try
    {
        var fresh = Path.Combine(root, "A", "session.lock");
        Directory.CreateDirectory(Path.GetDirectoryName(fresh)!);
        File.WriteAllText(fresh, "");
        var old = Path.Combine(root, "_index.lock");
        File.WriteAllText(old, "");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddHours(-1));
        File.WriteAllText(outside, "");
        File.SetLastWriteTimeUtc(outside, DateTime.UtcNow.AddHours(-1));

        var stale = SharedConfig.StaleLocks(root);
        Expect(stale.Count == 1 && stale[0] == old, "Yalnızca eski kilit listelenmeli");

        var blocked = false;
        try { SharedConfig.RemoveStaleLock(root, fresh); }
        catch (InvalidOperationException) { blocked = true; }
        Expect(blocked && File.Exists(fresh), "Yeni kilit silinmemeli");

        blocked = false;
        try { SharedConfig.RemoveStaleLock(root, outside); }
        catch (ArgumentException) { blocked = true; }
        Expect(blocked && File.Exists(outside), "Kök dışındaki dosya silinmemeli");

        SharedConfig.RemoveStaleLock(root, old);
        Expect(!File.Exists(old), "Eski kilit silinmeli");
    }
    finally
    {
        Directory.Delete(root, true);
        File.Delete(outside);
    }
}

static void CsvEscapes()
{
    var row = new IndexRow("=HYPERLINK(\"x\")", "OP;40", "SN-1", "Open", 1, 1, "", DateTime.Now,
        new[] { new IndexDefect(1, "+Dent", "Open", 0, "@SUM") }, "A. Yılmaz", DateTime.Now);
    var csv = CsvExport.Build(new[] { row });
    Expect(csv.Contains("\"'=HYPERLINK(\"\"x\"\")\""), "= ile başlayan hücre tırnaklanmalı");
    Expect(csv.Contains(";\"OP;40\";"), "Ayraç içeren hücre tırnak içine alınmalı");
    Expect(csv.Contains(";'+Dent;") && csv.Contains(";'@SUM"), "+ ve @ etkisizleşmeli");
    Expect(CsvExport.Cell("-5") == "'-5", "- ile başlayan hücre etkisizleşmeli");
}


// ───────────────────────── Senaryolar (adım adım izleme) ─────────────────────────

static void Trace(string step, Defect d)
{
    var last = d.Dispositions.LastOrDefault(x => x.Source != DecisionSource.System);
    var meas = $"D={d.Depth?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ?? "-"} " +
               $"W={d.Width?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ?? "-"} " +
               $"L={d.Length?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ?? "-"}";
    Console.WriteLine($"        · {step,-38} #{d.Id} {d.DefectType,-9} durum={DefectStateMachine.GetState(d),-18} " +
                      $"son karar={(last is null ? "-" : DefectSentence.DecisionLabel(last.Decision)),-36} {meas}");
}

static void CompareDirections()
{
    var before = new MeasurementSnapshot(0.12, 2.0, 3.0, null, null, "gri");
    var after = new Defect { Depth = 0.20, Width = 1.5, Length = 3.0, Color = "gri" };
    var lines = MeasurementCompare.Lines(before, after);
    Expect(lines.Any(l => l.Contains("Derinlik: 0.12 → 0.2 mm (artış)")), string.Join(" | ", lines));
    Expect(lines.Any(l => l.Contains("Genişlik: 2 → 1.5 mm (azalış)")), string.Join(" | ", lines));
    Expect(lines.Any(l => l.Contains("Uzunluk: 3 → 3 mm (aynı)")), string.Join(" | ", lines));
}

static void Scenario1_SameDefectThenConforms()
{
    var root = TempRoot();
    try
    {
        var store = new SessionStore(root);
        var (folder, _) = store.OpenOrCreate(SampleSession("SN-S1"));

        var d = store.AddDefect(folder, "Dent", x => { x.Depth = 0.12; x.Width = 2.0; });
        Trace("1. Kusur girildi", d);

        d = store.UpdateDefect(folder, d.Id, x => DefectStateMachine.Apply(x, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME"));
        Trace("2. Rework kararı", d);
        Expect(DefectStateMachine.GetState(d) == DefectState.AwaitingReinspect, "Rework sonrası muayene beklemeli");

        // Parça döndü: aynı kusur, derinlik artmış
        d = store.UpdateDefect(folder, d.Id, x => x.Depth = 0.20);
        var before = d.Dispositions.Last(x => x.Decision == Decision.Rework).Snapshot;
        Console.WriteLine("        · Karşılaştırma: " + string.Join(" | ", MeasurementCompare.Lines(before, d)));
        Expect(DefectStateMachine.GetState(d) == DefectState.AwaitingReinspect, "Sadece ölçüm değişince kusur kapanmamalı");

        d = store.UpdateDefect(folder, d.Id, x => DefectStateMachine.Apply(x, Decision.AfterReworkSameDefect, DecisionSource.Engineer, "CTP", "Derinlik arttı"));
        Trace("3. Reinspect: aynı kusur devam", d);
        Expect(DefectStateMachine.GetState(d) == DefectState.Open, "Aynı kusur Open'a dönmeli");

        d = store.UpdateDefect(folder, d.Id, x => DefectStateMachine.Apply(x, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME"));
        Trace("4. İkinci rework", d);

        d = store.UpdateDefect(folder, d.Id, x => { x.Depth = null; x.Width = null; });
        d = store.UpdateDefect(folder, d.Id, x => DefectStateMachine.Apply(x, Decision.AfterReworkConforms, DecisionSource.Engineer, "CTP"));
        Trace("5. Reinspect: uygun", d);
        Expect(DefectStateMachine.GetState(d) == DefectState.Closed, "Uygun kararı kapatmalı");
        Expect(d.Dispositions.Count == 4, $"4 karar kaydı beklenirdi, gelen: {d.Dispositions.Count}");
        Expect(SessionGate.CanComplete(store.LoadDefects(folder)), "Muayene kapanabilmeli");
    }
    finally { Directory.Delete(root, recursive: true); }
}

static void Scenario2_NewDefectChild()
{
    var root = TempRoot();
    try
    {
        var store = new SessionStore(root);
        var (folder, _) = store.OpenOrCreate(SampleSession("SN-S2"));

        var undercut = store.AddDefect(folder, "Undercut", x => x.Depth = 0.3);
        Trace("1. Undercut girildi", undercut);
        undercut = store.UpdateDefect(folder, undercut.Id, x => DefectStateMachine.Apply(x, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "OP40"));
        Trace("2. Rework (OP40)", undercut);

        var dent = store.ContinueAsChild(folder, undercut.Id, "Dent", DecisionSource.Engineer, "CTP", "Undercut gitti, dent oluştu", null);
        var all = store.LoadDefects(folder);
        undercut = all.First(x => x.Id == undercut.Id);
        dent = all.First(x => x.Id == dent.Id);
        Trace("3. Reinspect: yeni hata", undercut);
        Trace("   Child açıldı", dent);

        Expect(dent.DefectType == "Dent", "Child tipi Dent olmalı");
        Expect(DefectStateMachine.GetState(undercut) == DefectState.Superseded, "Undercut devredilmiş olmalı");
        Expect(!SessionGate.CanComplete(all), "Child açıkken muayene kapanmamalı");

        dent = store.UpdateDefect(folder, dent.Id, x => DefectStateMachine.Apply(x, Decision.Accept, DecisionSource.Engineer, "M. Kaya", reference: "KAB-77"));
        Trace("4. Dent kabul", dent);
        Expect(SessionGate.CanComplete(store.LoadDefects(folder)), "Child kapanınca muayene kapanabilmeli");
        Console.WriteLine("        · Cümle: " + DefectSentence.Describe(store.LoadDefects(folder).First(x => x.Id == undercut.Id)).Replace("\n", " / "));
    }
    finally { Directory.Delete(root, recursive: true); }
}

static void Scenario3_ReopenAndScrapFinal()
{
    var d = NewDefect(1, "Nick");
    DefectStateMachine.Apply(d, Decision.Accept, DecisionSource.Engineer, "M. Kaya");
    Trace("1. Kabul", d);
    DefectStateMachine.Apply(d, Decision.Reopen, DecisionSource.Engineer, "M. Kaya", note: "Kabul kağıdı yanlış parçaya aitti");
    Trace("2. Yeniden açıldı (not ile)", d);
    Expect(DefectStateMachine.GetState(d) == DefectState.Open, "Yeniden açma Open yapmalı");
    DefectStateMachine.Apply(d, Decision.Scrap, DecisionSource.Engineer, "M. Kaya");
    Trace("3. Hurda", d);
    Expect(!DefectStateMachine.Check(d, Decision.Reopen, DecisionSource.Engineer).Ok, "Hurda yeniden açılamamalı");
}

static void Scenario4_MrbRejected()
{
    var d = NewDefect(1, "Scratch");
    DefectStateMachine.Apply(d, Decision.SubmitMrb, DecisionSource.Engineer, "M. Kaya");
    Trace("1. MRB'ye gönderildi", d);
    Expect(!SessionGate.CanComplete(new[] { d }), "MRB bekleyen kusur muayeneyi kapatmamalı");
    DefectStateMachine.Apply(d, Decision.MrbRejected, DecisionSource.Mrb, "MRB", reference: "MRB-9");
    Trace("2. MRB reddi", d);
    Expect(d.Dispositions[^1].Decision == Decision.Scrap, "Otomatik hurda yazılmalı");
    Expect(SessionGate.CanComplete(new[] { d }), "Hurdadan sonra muayene kapanabilmeli");
}


static void VoidOnlyWithoutHistory()
{
    var d = NewDefect();
    DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME");
    DefectStateMachine.Apply(d, Decision.AfterReworkSameDefect, DecisionSource.Engineer, "CTP");
    Expect(DefectStateMachine.GetState(d) == DefectState.Open, "Kusur Open olmalı");
    Expect(!DefectStateMachine.Check(d, Decision.Void, DecisionSource.Operator).Ok, "Geçmişi olan kusur void edilmemeli");
    Expect(DefectStateMachine.Check(NewDefect(2), Decision.Void, DecisionSource.Operator).Ok, "Yeni kusur void edilebilmeli");
}


static void VoidNeedsNote()
{
    var d = NewDefect();
    var threw = false;
    try { DefectStateMachine.Apply(d, Decision.Void, DecisionSource.Operator, "A. Yılmaz"); }
    catch (InvalidOperationException) { threw = true; }
    Expect(threw, "Notsuz void reddedilmeli");
    DefectStateMachine.Apply(d, Decision.Void, DecisionSource.Operator, "A. Yılmaz", note: "Yanlış hata tipi");
    Expect(DefectStateMachine.GetState(d) == DefectState.Voided, "Notlu void kabul edilmeli");
}

static void MeasurementEditsLogged()
{
    var d = new Defect { Id = 1, DefectType = "Dent", Depth = 0.12 };
    var changed = MeasurementEditor.Apply(d, new MeasurementSnapshot(0.15, null, null, null, null, null), "A. Yılmaz");
    Expect(changed && d.Depth == 0.15, "Değer güncellenmeli");
    Expect(d.MeasurementEdits.Count == 1 && d.MeasurementEdits[0].Before.Depth == 0.12, "Önceki değer kayıtta olmalı");

    var again = MeasurementEditor.Apply(d, MeasurementEditor.Snapshot(d), "A. Yılmaz");
    Expect(!again && d.MeasurementEdits.Count == 1, "Değişiklik yoksa kayıt eklenmemeli");

    var html = ReportBuilder.Build(SampleSession(), new[] { d }, 1);
    Expect(html.Contains(System.Net.WebUtility.HtmlEncode("Ölçüm düzeltmeleri / Measurement corrections")), "Düzeltmeler raporda görünmeli");
}

static void DashboardFilter()
{
    IndexRow Row(string part, string op, string serial, int open, string status, params (string Type, string State)[] defects) =>
        new(part, op, serial, status, defects.Length, open, "", DateTime.Now,
            defects.Select((d, i) => new IndexDefect(i + 1, d.Type, d.State, 0)).ToList());

    var rows = new[]
    {
        Row("PD170", "OP40", "SN-A1", 1, "Open", ("Dent", "MrbPending")),
        Row("PD170", "OP50", "SN-A2", 0, "Completed", ("Scratch", "Closed")),
        Row("TS1400", "OP40", "SN-B1", 1, "Open", ("Undercut", "AwaitingReinspect")),
    };

    Expect(IndexFilter.Apply(rows, new IndexCriteria(Part: "pd170")).Count == 2, "Parça süzgeci");
    Expect(IndexFilter.Apply(rows, new IndexCriteria(Serial: "b1")).Count == 1, "Seri içerir araması");
    Expect(IndexFilter.Apply(rows, new IndexCriteria(DefectType: "Undercut")).Single().Serial == "SN-B1", "Hata tipi süzgeci");
    Expect(IndexFilter.Apply(rows, new IndexCriteria(State: IndexStateFilter.MrbPending)).Single().Serial == "SN-A1", "MRB süzgeci");
    Expect(IndexFilter.Apply(rows, new IndexCriteria(State: IndexStateFilter.Completed)).Single().Serial == "SN-A2", "Tamamlanan süzgeci");
    Expect(IndexFilter.Apply(rows, new IndexCriteria(Operation: "OP40", State: IndexStateFilter.HasOpen)).Count == 2, "Birleşik süzgeç");
}
