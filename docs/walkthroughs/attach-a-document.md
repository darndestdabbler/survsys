# Attach a document

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/AttachADocumentTests.cs`, and a step you cannot perform
is a defect in the application or in this document — never something to work
around.

## User story

> As an investigator, I attach a document to a case — the letter I sent, the
> laboratory report that arrived, the photograph somebody took — filed under
> what kind of document it is, so that the paperwork a case rests on is on the
> case, and anyone who opens the report sees the same file I attached.

This is the **Attachments** section of the legacy report form's **Notes** tab,
and it is inferred from two legacy screens, read-only here and never
modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → the CMR edit form → **Notes** tab | `../../../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md`, *Notes Tab → Layout*, and the screenshot `../../../nedss/docs/screenshots/session-012/16-notes-tab-initial.png` | The tab as three sections — **Attachments**, **Notes** and **Tasks** — with the Attachments section showing a **+ Attachment** button and the words **No attachments found** until something is attached. |
| The same tab, explored | the same document, *Attachments*, and `../../../nedss/docs/screenshots/session-012/17-notes-tab-explored.png` | What an attachment is made of: the `attachment` table's size, content type, filename, height, width and category against the report, and an `attachment_file` row holding the bytes themselves. |

Four differences from the legacy section are deliberate, and this walkthrough
describes what Survsys shows rather than what EpiTrax showed:

- **The panel opens inline, not as a dialog.** The legacy **+ Attachment**
  button opens an upload dialog over the screen; here the **Add an
  attachment** panel is always on the tab, under the list, as the note entry
  panel is. A dialog would be a mechanism this tab has nowhere else.
- **There is no download link and no delete.** The list is what the report
  holds: the filename, the category, the size, the content type and the day
  it arrived. The bytes are the API tier's to serve, and this tier cannot
  hand them on without either giving your browser that tier's address or
  proxying the file; an attachment is stored once and not removed.
- **The newest attachment is listed first.** What you just attached is the
  top row of the table, so it is the row you read.
- **The category is one of five, under the legacy's own labels.**
  **Correspondence**, **Laboratory results**, **Letter**, **X-ray** and
  **Other** — the five the legacy dialog offers, writing the five codes the
  API tier files an attachment under. The box starts on **Correspondence**.

**What may be attached.** The API tier stores nine kinds of file — PDF, plain
text, CSV, Word (both `.doc` and `.docx`), PNG, JPEG, GIF and TIFF — and
nothing larger than **30.0 MB**. A larger file is refused by the screen
before it is sent, with a message naming its size; a type the tier does not
store is refused by the tier and shown as the entry error. The file picker
offers those types to open, which is a convenience and not the check.

The legacy tab's **Notes** and **Tasks** sections, which sit below this one,
are not part of this walkthrough; *Work a task list* is the one about the
Tasks section.

## Before you start

You need all five of these. The last two are the ones people forget.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level. `Survsys.Api` is the only tier permitted to touch the
   database, and it is what brings an empty one up to level: start it with
   `Survsys__ApplyMigrations=true` and it migrates and then serves. Started
   without that switch, it refuses to serve a database below its migration
   level, which is the behaviour you want everywhere but a fresh machine.
2. **The person and the disease this walkthrough names, in that database.**
   `Survsys.IntegrationTests/Fixtures/conditions.sql` seeds the seven
   conditions, Listeriosis among them, and
   `Survsys.IntegrationTests/Fixtures/persons.sql` seeds the seven people,
   Jane Abbott among them; run the first and then the second against your
   database. The end-to-end suite seeds both itself; a database you brought
   up by hand does not have them until you run both. The sixteen
   investigating agencies are built into the API tier and need no seeding.
3. **The `Survsys.Api` tier running.** On a developer machine:
   `dotnet run --project Survsys.Api --urls http://localhost:5100`.
4. **The `Survsys.Web` tier running**, pointed at that API tier. On a
   developer machine: `dotnet run --project Survsys.Web --urls
   http://localhost:5000`, where `Survsys.Web/appsettings.Development.json`
   already points `Survsys:ApiBaseAddress` at `http://localhost:5100`. **The
   application tier's address is the one you open**, and on a developer
   machine that is <http://localhost:5000>. On a deployed site it is the
   address of the Survsys web site, which your deployment supplies.
5. **The file this walkthrough attaches, on the machine your browser runs
   on.** Step 1 writes it, because a file picker can only offer a file that
   is there.

This walkthrough **creates a report and attaches one file to it**. Perform it
twice and you have two reports on Jane Abbott, each with one attachment; that
is what the legacy system does too, and nothing here prevents it. It changes
nothing about the person, so the *Find a person* walkthrough reads the same
seven people before and after it. The end-to-end suite empties the
attachments, their bytes and the reports along with the people it reseeds on
every run, so a run always begins with none.

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

1. In a plain text editor, write a file named `exposure-letter.txt`
   containing this one line and no trailing newline, and save it somewhere
   you can find it again:

   ```
   Letter sent 2026-09-14: advise household contacts to watch for symptoms.
   ```

   Written exactly so, the file is **72 B**, and that is the size the screen
   shows you at steps 11 and 13. A file whose text you changed is a different
   size, and the size the screen shows is then yours rather than the one
   written here.
2. Open your browser and go to the application tier's address — on a
   developer machine, <http://localhost:5000>. In the coloured application
   bar across the top of the window, press **New CMR**.
3. In the box labelled **Last name**, type `Abbott`, then press **Search**.
4. The line above the table reads **1 person(s) found, showing 1.**, and the
   one row's **Last, First** reads **Abbott, Jane**.
5. Press **Create CMR** on that row.
6. Under the heading, the line begins **Step 2 of 2. Reporting on Abbott,
   Jane**.
7. Open the **Disease** list and choose **Listeriosis**. The list closes, and
   the **Disease** box now reads **Listeriosis**.
8. Open the **Investigating agency** list and choose **Salt Lake County**.
   The list closes, and the **Investigating agency** box now reads **Salt
   Lake County**.
9. Press **Save & Continue**. The report now exists, and the screen is its
   own form. In the header bar, **Patient name** reads **Abbott, Jane** and
   **Disease** reads **Listeriosis**. Note the address in your browser's
   address bar: it ends in `/event/edit/` and a number, and step 14 comes
   back to it.
10. Press the **Notes** tab. The tab opens on the **Attachments** section
    first, which reads **No attachments found.** — there is no table yet.
    Under it is the **Add an attachment** panel: a **Category** box reading
    **Correspondence**, a file picker beside it, a line reading **No file
    chosen.**, and an **Attach file** button.
11. Open the **Category** list and choose **Letter**. The list closes, and
    the **Category** box now reads **Letter**. Then press the file picker and
    choose the `exposure-letter.txt` you wrote at step 1. The line under the
    picker stops reading **No file chosen.** and reads
    **exposure-letter.txt (72 B)** instead.
12. Press **Attach file**. A green message appears at the top of the tab,
    reading **exposure-letter.txt has been attached.**
13. The **Attachments** section is now a table with the columns **File**,
    **Category**, **Size**, **Type** and **Attached**, and its one row reads,
    left to right: **exposure-letter.txt**, **Letter**, **72 B**,
    **text/plain**, and today's date written as `yyyy-mm-dd`.
14. Go back to the report's address — the one you noted in step 9 — by
    entering it in the address bar again, so that the screen is read afresh
    from the database rather than from what you typed, and press the
    **Notes** tab. The **Attachments** table has the one row, reading
    **exposure-letter.txt**, **Letter**, **72 B**, **text/plain** and
    today's date, exactly as in step 13. That is how you know the file
    reached the database.

## What you should see

A document attached to Jane Abbott's case morbidity report, filed and read
back:

- A report on **Abbott, Jane** for **Listeriosis**, opened in the two steps,
  with its **Notes** tab reading **No attachments found.** above an **Add an
  attachment** panel.
- After choosing the category **Letter** and the file
  `exposure-letter.txt`, the line under the picker reads
  **exposure-letter.txt (72 B)**, and **Attach file** answers
  **exposure-letter.txt has been attached.**
- The **Attachments** table with the one row: the filename, the category
  under its legacy label, the size, the content type the browser gave the
  file, and the day it was attached.
- Opening the report's address again and pressing **Notes** reads the same
  one row back from the database.

You now know how to put the paperwork a case rests on onto the case. If what
you saw differs from what is written above, the walkthrough has failed: say
so rather than adjusting what you did, because the promise this document
makes is that it is performable exactly as written.
