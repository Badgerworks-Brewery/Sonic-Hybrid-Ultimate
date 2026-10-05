Merge Q-DEV-issue-10: real Sonic CD script work on a live path

36 branches, 0 open PRs. `gh pr list --state all` shows all 5 historical PRs are already resolved -
1 merged, 4 closed - so there were no PRs to merge. Of 36 branches, 29 were already merged. Of the
7 unmerged, exactly one should be:

  Q-DEV-issue-10-1760287709   MERGED

    Hybrid-RSDK-Main/sonic-hybrid/Scripts/Global/ActFinish.txt, +475 -452. Adds
    SONICCD_SKIP_CUTSCENES as a configuration flag and rewrites ActFinish_NextStage for it. The
    only unmerged branch touching a path still in main, main has never touched that file since the
    branch point, and the content is not already present (reverse-apply fails). Merged with no
    conflicts, 0 conflict markers, symbol verified in place afterwards.

    Validated rather than assumed: scripts/check_script_entrypoints.py fails, but identically before
    and after - byte-for-byte the same 83 lines - so the merge did not cause it. That check reports
    70 stages with no bytecode while the bytecode path is active, which is the known Sonic CD
    converter gap, and it notes the text scripts are inert meanwhile.

The other 6 unmerged branches should NOT be merged, and it is worth being blunt about why rather
than merging them on request. Five edit paths that no longer exist:

  Q-DEV-issue-57       Hybrid-RSDK-Main/RSDKV4/RSDKV4/Userdata.hpp - directory deleted
  gitauto/issue-40    4 of 5 files gone; "Custom Client" is now "Custom-Client"
  gitauto/issue-42    edits "Hybrid-RSDK Main/" - the old spaced path; main's CI no longer
                      references it. Its CI change also removes the .NET setup step and the
                      Custom Client build, which is deleting functionality, not fixing anything.
  gitauto/issue-44    adds **/build/, **/bin/, **/obj/, **/Debug/ to .gitignore - main's .gitignore
                      already has **/build/ at line 9. Wholesale README replacement on top of that.
  patch-2             inverse of main's current file: its diff is the same 137-line churn in reverse
  patch-3             collapsed UseStageV3 from main's multi-line signature to one line, -43 lines
                      net. Main holds the expanded form; merging would revert newer work.

That last pair is the trap in "merge everything": patch-2 and patch-3 are *regressions* relative to
what main already contains, so a mechanical merge would look successful and undo work. Neither tip
is an ancestor of main, so git correctly reports them unmerged - but the reason is a directory
rename (SonicHybridRsdk.Generator -> Hybrid-RSDK-Main/SonicHybridRsdk.Generator) that git cannot
correlate, not missing work.

Verification note: `git log --all --follow` appeared to show patch-3's tip in main's history. It
is not - `--all` includes patch-3's own ref. `git merge-base --is-ancestor` is the check that
answers the actual question, and it says no.

Branches left in place, nothing force-merged.
