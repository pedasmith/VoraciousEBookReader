# How to update the Zipped database file

The Voracious EBook Reader ships with a pre-made database with a copy of a more or less up-to-date Gutenberg catalog. Every time I ship, I want to update that catalog, but without disturbing all my notes and settings. 

This is how I do it.

# Exact steps

Most of these steps use the menu in Programming Menu. This is only availble in Debug mode.

1. Find the current database directory location (**local**). The database is BookData.db
2. Copy the database (e.g., to BookData - Copy.db)
3. Download the latest Gutenberg catalog using the Download Catalog menu. You'll need to pick a location for the file. This should take maybe 10 seconds. The downloaded file (zipped) is about 80 megabytes.
4. **Reset your bookmark directory**. This is critical! Set it to e.g. C:\temp\2021\bookmarkTemp2 . Remember your old directory :-)
5. Rebuild the database using the Rebuild Clean Database menu. There are about 60K books.

6. Copy the the InitialBookData.db to a temp directory, rename it to plain BookData.db and create a ZIP file (right-click / send to / zipped folder) It should be about 40k in size.
7. Rename the BookdData.zip to InitialBookdData.zip (yes, this is a bit awkward -- the zip file is called InitialBookData.zip and it has a single file, BookdData.db)
8. Move the ZIP file to the Assets folder, replacing the original.
9. Test by deleting the BookData.db file (remember you saved a copy in step 2!) and run the app. It should automatically copy the .ZIP DB in. You'll know because you'll get the Preparing dozens from FREE ebooks for first use
99. Remember to switch back to your original bookmark directory!
```SQL
-- Do not modify __EFMigrationsHistory
-- Do not modify sqlite_sequence

--
-- Here are (commented out) queries for the data that shouldn't be in the database
--
--SELECT * FROM BookNavigationData LIMIT 50
--SELECT * FROM BookNotes LIMIT 500
--SELECT * FROM Books WHERE BookId NOT LIKE "ebooks/%" LIMIT 500
--SELECT * FROM DownloadData WHERE FilePath NOT LIKE "PreinstalledBooks%" LIMIT 500
--SELECT * FROM FilenameAndFormatData WHERE BookId NOT LIKE "ebooks/%" LIMIT 500
--SELECT * FROM Person WHERE BookDataBookId NOT LIKE "ebooks/%" LIMIT 500
--SELECT * FROM UserNote LIMIT 500
--SELECT * FROM UserReview LIMIT 500
```