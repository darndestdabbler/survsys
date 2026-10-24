# Remove an attachment

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/RemoveAnAttachmentTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As an investigator, when a file attached to a case should not be there —
> attached to the wrong case, or superseded by a better copy — I remove it
> from the case, after being asked whether I mean to, so that what the case
> holds is the paperwork it rests on and nothing else.

This is the **Attachments** section of the legacy report form's **Notes** tab,
and the delete its attachments offer, inferred from the legacy documentation,
read-only here and never modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → the CMR edit form → **Notes** tab | `../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md`, *Notes Tab → Layout* and *Attachments* | The tab's **Attachments** section, reading **No attachments found** when the report holds none, and an attachment as an `attachment` row against the report with its bytes in an `attachment_file` row. |
| EpiTrax → the CMR edit form → **Options** menu | `../nedss/docs/functional/epitrax-cmr-deep-editing.md`, *7. CMR Header Bar (persistent across tabs)* | Attachments and deletion as actions taken on a report from its own form. |

Two things this walkthrough describes as Survsys does them:

- **Delete is on the attachment's own row.** Each row of the **Attachments**
  table ends in a **Delete** button, shown to a user holding `edit_cases`.
- **Delete asks first, and cannot be undone.** A box asks **Delete this
  attachment?**, and pressing its **Delete** removes the attachment and the
  file it holds; there is no restoring it. Its **Cancel** leaves the
  attachment as it was.

Attaching the file is the subject of *Attach a document*; this walkthrough
attaches one only so that it has one to remove.

## Before you start

You need all six of these. The fifth is the one people forget.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level, brought up by `Survsys.Api` started with
   `Survsys__ApplyMigrations=true` as the other walkthroughs describe.
2. **The person and the disease this walkthrough names, in that database.**
   Run `Survsys.IntegrationTests/Fixtures/conditions.sql` and then
   `Survsys.IntegrationTests/Fixtures/persons.sql` against it: they seed
   Listeriosis and Jane Abbott. The end-to-end suite seeds both itself.
3. **The `Survsys.Api` tier running.** On a developer machine:
   `dotnet run --project Survsys.Api --urls http://localhost:5100`.
4. **The `Survsys.Web` tier running**, pointed at that API tier. On a
   developer machine: `dotnet run --project Survsys.Web --urls
   http://localhost:5000`. **The application tier's address is the one you
   open**, and on a developer machine that is <http://localhost:5000>.
5. **The file this walkthrough attaches, on the machine your browser runs
   on.** Step 1 writes it, because a file picker can only offer a file that
   is there.
6. **Signed in, as a user whose role holds every privilege**, the way the
   other walkthroughs describe, and then begin at step 1.

This walkthrough **creates a report, attaches one file to it and removes that
file again**. Perform it twice and you have two reports on Jane Abbott, each
with no attachment. The end-to-end suite empties the attachments, their bytes
and the reports on every run, so a run always begins with none.

**To watch this walkthrough performed instead of performing it yourself**, run

```
dotnet test Survsys.E2ETests/Survsys.E2ETests.csproj
```

That suite starts both tiers on ports it finds free, seeds the conditions and
the people, writes the file of step 1 into its own temporary folder, and
performs the steps below in a real browser. It records video of every test it
runs, and writes the recordings to
`Survsys.E2ETests/bin/<configuration>/net10.0/videos` as `.webm` files.

## Steps

1. In a plain text editor, write a file named `contact-list.txt` containing
   this one line and no trailing newline, and save it somewhere you can find
   it again:

   ```
   Household contacts: two adults, one child.
   ```

   Written exactly so, the file is **42 B**.
2. Open your browser and go to the application tier's address — on a
   developer machine, <http://localhost:5000>. In the coloured application
   bar across the top of the window, press **New CMR**.
3. In the box labelled **Last name**, type `Abbott`, then press **Search**.
   The line above the table reads **1 person(s) found, showing 1.**, and the
   one row's **Last, First** reads **Abbott, Jane**.
4. Press **Create CMR** on that row. Open the **Disease** list and choose
   **Listeriosis**, then open the **Investigating agency** list and choose
   **Salt Lake County**.
5. Press **Save & Continue**. The screen is the report's own form: in the
   header bar, **Patient name** reads **Abbott, Jane** and **Disease** reads
   **Listeriosis**. Note the address in your browser's address bar: step 10
   comes back to it.
6. Press **Edit** at the top of the report, then press the **Notes** tab. The
   **Attachments** section reads **No attachments found.**
7. In the **Add an attachment** panel, leave **Category** reading
   **Correspondence**, press the file picker and choose the
   `contact-list.txt` you wrote at step 1, and press **Attach file**. A green
   message reads **contact-list.txt has been attached.**, and the
   **Attachments** table's one row reads **contact-list.txt**,
   **Correspondence**, **42 B**, **text/plain** and today's date, and ends in
   a **Delete** button.
8. Press **Delete** on that row. A box opens, headed **Delete this
   attachment?**, reading **contact-list.txt and its file are removed, and
   cannot be restored.**, with a **Cancel** and a **Delete** button.
9. Press **Delete** in the box. The box closes, the table is gone, and the
   **Attachments** section reads **No attachments found.** again.
10. Go back to the report's address — the one you noted in step 5 — by
    entering it in the address bar again, so that the screen is read afresh
    from the database, and press the **Notes** tab. The **Attachments**
    section reads **No attachments found.** That is how you know the
    attachment is gone from the database and not only from the screen.

## What you should see

A file attached to Jane Abbott's case morbidity report, and removed:

- The **Attachments** table's one row for **contact-list.txt**, ending in
  **Delete**.
- A box asking **Delete this attachment?** before anything is removed.
- **No attachments found.** once it is confirmed, and again when the report
  is read afresh.

You now know how to take a file off a case. If what you saw differs from what
is written above, the walkthrough has failed: say so rather than adjusting
what you did, because the promise this document makes is that it is
performable exactly as written.
