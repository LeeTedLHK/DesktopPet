# DesktopPet P15 design

The user requests continuing optimization and useful feature updates, with earlier authorization to proceed without approval pauses. Extend the existing diary and focus flows, preserving all preferences, real data, art and offline operation. Deliver0.9.6/P15; no new dependencies, settings defaults or raster changes.

## Diary lookup and selection export

Add keyword search, record-kind and date-range filters to the diary section. Filters combine with AND. Keyword matches entry text or its readable Chinese kind label, ordinal case-insensitive, with surrounding whitespace ignored. Blank search shows all matching records. Ranges are All, Today, Last7Days(today-6 through today) and Last30Days(today-29 through today), based on the entry's stored LocalDate. Relative ranges exclude future local dates and saturate at DateOnly.MinValue. All retains every stored record. Invalid range falls back to All; an undefined kind matches none.

Results are immutable, sorted newest-first by absolute timestamp, retaining entry identity and preserving the input. UI shows matching/total count and a useful empty hint, supports clearing filters, and keeps filters/selection during companion ticks and new diary records. Rebuild only after filter changes, a changed journal snapshot or local-date rollover; countdown ticks cannot reset list selection/scrolling.

Keep full TXT export unchanged. Add a separate export-current-results action, disabled when no result. Its immutable filter captures the displayed local date and text/kind/range. Export queries one normalized current profile snapshot, clearly labels criteria and selected/total entry counts, keeps lifetime companion totals identified as totals, uses chronological rows, preserves original live input, and shares the verified atomic writer. A canceled dialog makes no change. Failed replacement retains target and cleans only owned temp. No new overload named Create (existing reflection/native contracts use its exact4-argument signature).

Core contracts: namespace DesktopPet.Journal; enum JournalDateRange {All,Today,Last7Days,Last30Days}; record JournalFilter(string SearchText, JournalEntryKind? Kind, JournalDateRange Range, DateOnly Today); static JournalQuery.Filter(IEnumerable<JournalEntry> entries,JournalFilter filter):IReadOnlyList<JournalEntry>, Description(JournalFilter):string, KindLabel(JournalEntryKind):string. Export API CreateFiltered(string destination,string petName,PetProfile profile,DateTimeOffset createdUtc,JournalFilter filter):void. Existing Create4/BuildText3 remain.

## Focus convenience and visibility

Add preset buttons25/5,45/10,60/10 minutes. Each emits one settings edit changing both durations atomically; current active/paused focus phase, deadline and manual game are preserved. Starting the next session uses new preferences. Preserve current custom combo values and existing start/pause/stop controls.

Show a phase progress bar and estimated local end time for running Focus/Break. Pause removes the obsolete end estimate and retains progress; resume uses the new deadline. Break has its own duration/progress. Completed displays100%, Stopped0% and no estimate. Read actual session duration, never recompute from edited next-session preferences. No changes to focus phase transitions/rewards or reminder behavior.

Core FocusTimer exposes Duration:TimeSpan (active phase duration, otherwise zero), EstimatedEndUtc:DateTimeOffset? (running active deadline in UTC, null when paused/inactive), Progress(DateTimeOffset now):double in0..1 (Completed1, Stopped0, active remaining/duration with existing observed-time rollback protection). SettingsWindow.SetFocusDetails(FocusTimer timer,DateTimeOffset now) renders the values; ApplicationService calls it during UpdateCompanion.

## Delivery and verification

Source baseline artifacts/verification/p15-source-clean-before.zip. No Git repository. Root alone runs serialized builds/tests; agents edit disjoint domains. Tests use temporary profiles, never write fixtures to user data. Keep175 assets and all P6–P14 release ZIPs unchanged.

Observe compatible semantic RED before implementation, then focused/full offline and native checks, full JournalChecks (query touches diary domain), fresh source review and real WPF renders. Skin implementation/assets are unchanged; existing full skin results from P14 remain relevant, while native skin integration and release asset hashes are rerun. Snapshot actual current data before stopping P14 for native hotkey checks. Publish and start P15, verify private runtime/single-instance, original diary/counters/preferences and final responding process. Document hardware and save-dialog verification limits.
