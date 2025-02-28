# Qiqqa Internals :: Extracting the text from PDF documents

# The Qiqqa OCR *background* process 

<title-runner>(as per 2020-03-22)</title-runner>

Before we dive in, there's one important question to ask:


## Given a PDF, *what* does Qiqqa store on disk?


### TL;DR

1. the original PDF, *renamed* using the *content hash*.
2. the extracted text (as a series of words plus box coordinates in a proprietary text format)



### ☞ The long answer to this question 🙉🎉


<details>
  <summary>
    <b>(Click here to unfold)</b>
  </summary>

<!-- empty paras to improve display on github -->
<br>



> #### Does it matter where the PDF is coming from?
>
> It does not matter *how* Qiqqa obtained the incoming PDF document, be it by "watch folder" directory scanning, website sniffer download, drag&drop or other means to import: all incoming PDFs are processed the same way.
>
> Some **metadata** bits may be different: a source URL may be saved on Sniffer download or alike, but that's about it.

- The incoming **original PDF** is copied to the Qiqqa Library **document store**, which is located in the `<LibraryID>/documents/` directory tree.

  The PDF **content** is hashed (using a [SHA1 derivative](https://github.com/jimmejardine/qiqqa-open-source/blob/0b015c923e965ba61e3f6b51218ca509fcd6cabb/Utilities/Files/StreamFingerprint.cs#L14)) to produce a unique identifier for this particular PDF **content**. That hash is used throughout Qiqqa for indexing *and* is to *name* the cached version of the incoming PDF, using a simple yet effective distribution scheme to help NTFS/file-system performance for large libraries: the first character of the hash is also used as a *subdirectory* name. 
  
  Example path for a PDF file stored in the `Guest` Qiqqa Library:

  ```
    base/Guest/documents/D/DA7B8FDA82E6D7465ADC7590EEC0C914E955C5B8.pdf
  ```

- The **extracted text** is saved in a Qiqqa-global store at `base/ocr/` using a similar file-system performance scheme as for the PDF  file itself.

  
  Example paths for the OCR output cached for the same PDF file as shown above:

  ```
   base/ocr/DA/DA7B8FDA82E6D7465ADC7590EEC0C914E955C5B8.pagecount.0.txt
    base/ocr/DA/DA7B8FDA82E6D7465ADC7590EEC0C914E955C5B8.text.4.txt
    base/ocr/DA/DA7B8FDA82E6D7465ADC7590EEC0C914E955C5B8.textgroup.001_to_020.txt
    base/ocr/DA/DA7B8FDA82E6D7465ADC7590EEC0C914E955C5B8.textgroup.021_to_040.txt
  ```
  
  > Note that in this example, we apparently had a PDF which had its page 4 OCRed using `tesseract` (a.k.a. the **SINGLE** process), while the other 20+ pages got extracted using `mupdf` (a.k.a. the **GROUP** process): apparently the given PDF was a text-based PDF which *possibly* an empty page or a full-page graphic without embedded text on page 4.
  >
  > See the process description below for more info.
  
  The **TEXT DATA** stored in these 'ocr' files uses a custom text format, where each word is listed on a separate line and accompanied by a set of coordinates describing the rectangle of its location within the page.
  
  Example OCR text file snippet:
  
  ```
    # Generated by: QiqqaOCR.
    # Version: 3
    # List source: PDFText
    # System culture: en-US
    @PAGE: 1

    0.62114,0.04798,0.11382,0.01641:USOO695.2431B1

    0.12683,0.08586,0.02602,0.02904:(12)

    0.15935,0.08586,0.08455,0.02904:United

    0.25366,0.08586,0.07480,0.02904:States

    0.33984,0.08586,0.07967,0.02904:Patent

    0.52683,0.08586,0.02602,0.02904:(10)

    0.55935,0.08586,0.05528,0.02904:Patent

    0.62114,0.08586,0.03415,0.02904:No.:

    0.69593,0.08586,0.03089,0.02904:US

    0.73333,0.08586,0.09106,0.02904:6,952,431

    0.83252,0.08586,0.02602,0.02904:B1

    0.15772,0.10732,0.04553,0.02399:Dally

    0.20813,0.10732,0.01626,0.02399:et

    0.22927,0.10732,0.02276,0.02399:al.

    0.52683,0.10732,0.02602,0.02399:(45)

    0.55935,0.10732,0.03902,0.02399:Date

    0.60325,0.10732,0.02114,0.02399:of

    0.62764,0.10732,0.05854,0.02399:Patent:

    0.75772,0.10732,0.03740,0.02399:Oct.

    0.79837,0.10732,0.01626,0.02399:4,

    0.81789,0.10732,0.03902,0.02399:2005

    0.12683,0.14899,0.02602,0.01641:(54)

    0.16585,0.14899,0.05528,0.01641:CLOCK

    0.22439,0.14899,0.10569,0.01641:MULTIPLYING

    0.33333,0.14899,0.11707,0.01641:DELAY-LOCKED

    0.53821,0.14899,0.05366,0.01641:6,037,812

    0.59675,0.14899,0.01138,0.01641:A

    0.63577,0.14899,0.03740,0.01641:3/2000

    0.68293,0.14899,0.03902,0.01641:Gaudet

    0.72683,0.14899,0.08455,0.01641:.......................

    0.81463,0.14899,0.04390,0.01641:327/116

    0.16748,0.16035,0.04228,0.01641:LOOP

    0.21301,0.16035,0.03089,0.01641:FOR

    0.24878,0.16035,0.03902,0.01641:DATA

    0.29106,0.16035,0.14146,0.01641:COMMUNICATIONS

    0.53821,0.16035,0.05366,0.01641:6,043,717

    0.59675,0.16035,0.01138,0.01641:A

    0.63577,0.16035,0.03740,0.01641:3/2000

    0.68293,0.16035,0.02764,0.01641:Kurd

    0.71545,0.16035,0.09919,0.01641:...........................

    0.82114,0.16035,0.01463,0.01641:33
  ```
  
  As you can already see, a 'word' here is not always in accordance of the human purview of the meaning of 'word', e.g. the 'word' `...........................` at the end of the snippet there.
  
  Qiqqa [applies a few filters to this data](https://github.com/jimmejardine/qiqqa-open-source/blob/1ef3403788d2b2d5efcc08dc244a60d1694f5453/Qiqqa/DocumentLibrary/DocumentLibraryIndex/LibraryIndex.cs#L629-L638) before it is injected into the `Lucene` search index database.

</details>






## The Qiqqa OCR internal workflow


### TL;DR

1. background process Stage 1: `mupdf` — extract text from PDF.
   <br>
   Go to next step when you fail.
2. background process Stage 2: `tesseract`/OCR — extract text from PDF *page images*.
   <br>
   Go to next step when you fail.
3. v80 and before: give it the run-around. For ever.
   <br>
   v82+: Fake it and *shut up* until we *improve*.

Other Qiqqa (background) processes *will* impact OCR activity: the Lucene text search index and metadata inference systems *want* OCR data and don't stop until they *do*.



### ~~TL;DR~~            ☞ 🙥—— The whole story ——🙧 🙉🎉

<!-- 🙚 🙘 🙛 🙙 🙞 🙜 🙟 🙝 🙠 🙡 🙢 🙣 🙤 🙥 🙦 🙧 -->

<details>
  <summary>
    <b>(Click here to unfold)</b>
  </summary>

<!-- empty paras to improve display on github -->
<br>

<!-- ### The long answer to that question -->


Once the background task gets around to it, the PDF is OCRed if this has not happened yet. 
This is generally detected by checking whether the expected OCR data for page 1 is available.
  
> The correct(er) answer here is: *it depends*: several conditions exist (e.g. when the document is viewed by the user in a Qiqqa panel) when *all pages* of the document are requested and any of them missing will (re)trigger the OCR process.
>
> See all the invocations of [the `GetOCRText()` method](https://github.com/jimmejardine/qiqqa-open-source/blob/1ef3403788d2b2d5efcc08dc244a60d1694f5453/Qiqqa/Documents/PDF/PDFRendering/PDFRenderer.cs#L98) in the Qiqqa source code.


### Qiqqa OCR Stage 1: The Extract Attempt (= [the `"GROUP"` call](https://github.com/jimmejardine/qiqqa-open-source/blob/a50888e836224e1d293457c8cd9a59cfef403bf7/Qiqqa/Documents/PDF/PDFRendering/PDFTextExtractor.cs#L652))

First, Qiqqa attempts to [extract text from the PDF without OCR-ing it, using the `mupdf` tool](https://github.com/jimmejardine/qiqqa-open-source/blob/1ef3403788d2b2d5efcc08dc244a60d1694f5453/QiqqaOCR/TextExtractEngine.cs#L178): this should deliver for all PDFs which are not 'page image based'.

The text data collected this way is stored in proprietary format text files, up to  20 pages per file, in the `ocr` global directory tree.

Example paths:

```
  base/ocr/DA/DA7B8FDA82E6D7465ADC7590EEC0C914E955C5B8.textgroup.001_to_020.txt
  base/ocr/DA/DA7B8FDA82E6D7465ADC7590EEC0C914E955C5B8.textgroup.021_to_040.txt
```
  
However, when this fails to produce any text, Qiqqa *will* trigger a Stage 2 OCR action for each of those pages of the PDF which do not produce any text this way.

> In actual practice, this means many text-based PDFs will have an OCR job running for them anyway when there's an empty page, or one with only some graphics, or a title page which did not deliver any text by way of `mupdf`.


### Qiqqa OCR Stage 2: The OCR Attempt (= [the `"SINGLE"` call](https://github.com/jimmejardine/qiqqa-open-source/blob/a50888e836224e1d293457c8cd9a59cfef403bf7/Qiqqa/Documents/PDF/PDFRendering/PDFTextExtractor.cs#L711))

This background job is executed for every single page in the PDF which  did not deliver any text in the Stage 1 process above.

By now, Qiqqa assumes the PDF is image based and requires a true OCR process to obtain the text from the PDF page. 

Currently it uses the Sorax PDF library to render the PDF page<b id="Stage2OCR1">[<sup>†</sup>](#SoraxWoes)</b>, which is then [fed into Tesseract v3 for OCR-ing](https://github.com/jimmejardine/qiqqa-open-source/blob/1ef3403788d2b2d5efcc08dc244a60d1694f5453/QiqqaOCR/OCREngine.cs#L230). Region detection is performed by Qiqqa [proprietary logic](https://github.com/jimmejardine/qiqqa-open-source/blob/1ef3403788d2b2d5efcc08dc244a60d1694f5453/QiqqaOCR/OCREngine.cs#L251) and passed into Tesseract.[<sup id="user-content-stage2ocr2">‡</sup>](#TesseractWoes) 

Again, the expected OCR output is a set of 'words' and box coordinates pointing at the position of these OCR-ed words in the page. This information is stored on a per-page basis in that same  proprietary Qiqqa text format.

Example path:

```
  base/ocr/DA/DA7B8FDA82E6D7465ADC7590EEC0C914E955C5B8.text.4.txt
```


### What happens when Stage 2 (and Stage 1) has failed...? 🥶 😱

Qiqqa v80 (and commercial Qiqqa v79 at least) will then go and re-queue the same OCR job(s) after a while since no OCR text cache files could be produced (the page(s) did not produce a single word after all and the Qiqqa text OCR files are not supposed to be *empty*!

The result here is that Qiqqa will continuously re-attempt the same (failing) OCR activity for these troublesome pages in the background, loading the machine indefinitely. 🥶 😱


#### v82 *experimental* releases: Stage 3: Faking It (= [the `"SINGLE-FAKE"` call](https://github.com/GerHobbelt/qiqqa-open-source/blob/bc80c1c07b0beda99e99021029c875bde36e2bd1/Qiqqa/Documents/PDF/PDFRendering/PDFTextExtractor.cs#L793))

Qiqqa v82 (and later, I expect 😉) has added a Stage 3: when Stage 1 and Stage 2 have failed to deliver any words for the given page, then we are sure that either the PDF page has no text or at the very least Qiqqa is currently incapable of retrieving any text on that page. To prevent Qiqqa from running heavy CPU loading OCR tasks indefinitely (= until you quit the application), we "fake it" by storing a specific "magic sequence" in the Stage 2 OCR text cache file. 🤷

> Future versions of Qiqqa SHOULD have improved OCR capabilities and will find and detect these "faked pages" and erase them before re-doing the OCR process then. But that is, at this very moment (2020-03-22 AD) still future music: [#160](https://github.com/jimmejardine/qiqqa-open-source/issues/160)




## Other Qiqqa background processes which use and influence the OCR process' behaviour


### The Lucene Text SearchIndex Update Process

[Another Qiqqa background process](https://github.com/jimmejardine/qiqqa-open-source/blob/0b015c923e965ba61e3f6b51218ca509fcd6cabb/Qiqqa/Common/BackgroundWorkerDaemonStuff/BackgroundWorkerDaemon.cs#L231) updates the Qiqqa text search index, which is powered by LuceneNET.

This process walks through your Qiqqa Library/Libraries and checks whether the OCR process for each PDF document has completed.

> Incidentally, this background-running check will (re)trigger the OCR process if the answer to that question is not a resounding *yes*!

When the OCR text data is new, the data is collected and [fed into the Lucene search index database](https://github.com/jimmejardine/qiqqa-open-source/blob/1ef3403788d2b2d5efcc08dc244a60d1694f5453/Qiqqa/DocumentLibrary/DocumentLibraryIndex/LibraryIndex.cs#L646). See the [`AddDocumentPage()`](https://github.com/jimmejardine/qiqqa-open-source/blob/a50888e836224e1d293457c8cd9a59cfef403bf7/Utilities/Language/TextIndexing/LuceneIndex.cs#L180) and [`IncrementalBuildNextDocuments()`](https://github.com/jimmejardine/qiqqa-open-source/blob/1ef3403788d2b2d5efcc08dc244a60d1694f5453/Qiqqa/DocumentLibrary/DocumentLibraryIndex/LibraryIndex.cs#L466) methods' code for more. Also check out the use of the `PDFDocumentInLibrary.pages_already_indexed` and `PDFDocumentInLibrary.finished_indexing` attribute members; any retry attempts are relaxed via the `PDFDocumentInLibrary.last_indexed` attribute member: [(def)](
https://github.com/jimmejardine/qiqqa-open-source/blob/1ef3403788d2b2d5efcc08dc244a60d1694f5453/Qiqqa/DocumentLibrary/DocumentLibraryIndex/PDFDocumentInLibrary.cs#L13) & [(use)](https://github.com/jimmejardine/qiqqa-open-source/blob/1ef3403788d2b2d5efcc08dc244a60d1694f5453/Qiqqa/DocumentLibrary/DocumentLibraryIndex/LibraryIndex.cs#L466).



### Ooh! *Almost forgot!* The metadata inference process!

[Yet another background task](https://github.com/jimmejardine/qiqqa-open-source/blob/0b015c923e965ba61e3f6b51218ca509fcd6cabb/Qiqqa/DocumentLibrary/MetadataExtractionDaemonStuff/MetadataExtractionDaemon.cs) goes through your libraries' documents and attempts to infer a *title*, *author*, [*abstract*](https://github.com/jimmejardine/qiqqa-open-source/blob/0b015c923e965ba61e3f6b51218ca509fcd6cabb/Qiqqa/Documents/PDF/PDFControls/Page/Tools/PDFAbstractExtraction.cs#L11) and other *metadata* from the OCR-ed text data for the given PDF. This MAY also (re)trigger the OCR process when the text data has not been produced before. (By now you'll surely understand why the v82 "Stage 3" = "SINGLE-FAKE" hack was invented...)

This *inferred* metadata is shown and used by Qiqqa when there is no BibTeX metadata provided by the user (via Qiqqa Sniffer or manually entry):  the BibTeX metadata is deemed [*superior* and *overriding*](https://github.com/jimmejardine/qiqqa-open-source/blob/1ef3403788d2b2d5efcc08dc244a60d1694f5453/Qiqqa/Documents/PDF/PDFDocument.cs#L604). This metadata is also added to the Lucene search index to help users dig up articles by \[parts of the\] title, author, etc. (Most of the relevant source code can be spotted in the [`PDFMetadataInferenceFromPDFMetadata`](https://github.com/jimmejardine/qiqqa-open-source/blob/0b015c923e965ba61e3f6b51218ca509fcd6cabb/Qiqqa/Documents/PDF/MetadataSuggestions/PDFMetadataInferenceFromPDFMetadata.cs) and [`PDFMetadataInferenceFromOCR`](https://github.com/jimmejardine/qiqqa-open-source/blob/0b015c923e965ba61e3f6b51218ca509fcd6cabb/Qiqqa/Documents/PDF/MetadataSuggestions/PDFMetadataInferenceFromOCR.cs) classes.)





</details>




<!-- HR -->
<br><br>
<p align="center" style="margin-top: 50px"><img src="../assets/divider-end.svg" width="200"></p>
<br><br><br>




<b id="SoraxWoes">†</b>: The Sorax library doesn't support some 'protected' PDFs and renders those pages as white-on-white, resulting in a completely blank view inside Qiqqa. See also these woes viewing PDFs in Qiqqa:

- https://getsatisfaction.com/qiqqa/topics/pdfs_stop_displaying_blank_pages#reply_17983571
- https://github.com/jimmejardine/qiqqa-open-source/issues/136

> At the time of this writing, I know/strongly suspect almost all these white-pages-rendered-only problems are due to bugs in the  Sorax lib as  I have many PDFs in my collection suffering from this. 🤬

[⤣](#user-content-stage2ocr1)

<b id="TesseractWoes">‡</b>: Your family name doesn't have to be [Statler or Waldorf](https://en.wikipedia.org/wiki/Statler_and_Waldorf) to have plenty to complain about that region detection logic too: [#135](https://github.com/jimmejardine/qiqqa-open-source/issues/135). And then there's the old Tesseract which needs some assist as well: [#160](https://github.com/jimmejardine/qiqqa-open-source/issues/160) and [one other bit mentioned in #135](https://github.com/jimmejardine/qiqqa-open-source/issues/135#issuecomment-569827317).

However, it's not all that bleak when your research does not include diving into old/historic documents and/or PDFs published by companies: many modern scientific papers are published in a PDF format which can be grokked by `mupdf` just fine — though here I have found that quite a few PDFs which *appear* to have been produced by some unidentified TeX variants *do* cause trouble in Stage 1 (`"GROUP"`) and produce some crap of their own: [#86](https://github.com/jimmejardine/qiqqa-open-source/issues/86)

[⤣](#user-content-stage2ocr2)

