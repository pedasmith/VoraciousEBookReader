using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EF_Is_Just_Plain_Dumb.Migrations
{
    public partial class CreateBookData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookNavigationData",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BookId = table.Column<string>(nullable: true),
                    NCatalogViews = table.Column<int>(nullable: false),
                    NSwipeRight = table.Column<int>(nullable: false),
                    NSwipeLeft = table.Column<int>(nullable: false),
                    NReading = table.Column<int>(nullable: false),
                    NSpecificSelection = table.Column<int>(nullable: false),
                    CurrSpot = table.Column<string>(nullable: true),
                    CurrStatus = table.Column<int>(nullable: false),
                    TimeMarkedDone = table.Column<DateTimeOffset>(nullable: false),
                    FirstNavigationDate = table.Column<DateTimeOffset>(nullable: false),
                    MostRecentNavigationDate = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookNavigationData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BookNotes",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BookId = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookNotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DownloadData",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BookId = table.Column<string>(nullable: true),
                    FilePath = table.Column<string>(nullable: true),
                    FileName = table.Column<string>(nullable: true),
                    CurrFileStatus = table.Column<int>(nullable: false),
                    DownloadDate = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserReview",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BookId = table.Column<string>(nullable: true),
                    CreateDate = table.Column<DateTimeOffset>(nullable: false),
                    MostRecentModificationDate = table.Column<DateTimeOffset>(nullable: false),
                    NStars = table.Column<double>(nullable: false),
                    Review = table.Column<string>(nullable: true),
                    Tags = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserReview", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserNote",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BookId = table.Column<string>(nullable: true),
                    CreateDate = table.Column<DateTimeOffset>(nullable: false),
                    MostRecentModificationDate = table.Column<DateTimeOffset>(nullable: false),
                    Location = table.Column<string>(nullable: true),
                    Text = table.Column<string>(nullable: true),
                    Tags = table.Column<string>(nullable: true),
                    Icon = table.Column<string>(nullable: true),
                    BackgroundColor = table.Column<string>(nullable: true),
                    ForegroundColor = table.Column<string>(nullable: true),
                    SelectedText = table.Column<string>(nullable: true),
                    BookNotesId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNote", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserNote_BookNotes_BookNotesId",
                        column: x => x.BookNotesId,
                        principalTable: "BookNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Books",
                columns: table => new
                {
                    BookId = table.Column<string>(nullable: false),
                    BookSource = table.Column<string>(nullable: true),
                    BookType = table.Column<int>(nullable: false),
                    Description = table.Column<string>(nullable: true),
                    Imprint = table.Column<string>(nullable: true),
                    Issued = table.Column<string>(nullable: true),
                    Title = table.Column<string>(nullable: true),
                    TitleAlternative = table.Column<string>(nullable: true),
                    Language = table.Column<string>(nullable: true),
                    LCSH = table.Column<string>(nullable: true),
                    LCCN = table.Column<string>(nullable: true),
                    PGEditionInfo = table.Column<string>(nullable: true),
                    PGProducedBy = table.Column<string>(nullable: true),
                    PGNotes = table.Column<string>(nullable: true),
                    BookSeries = table.Column<string>(nullable: true),
                    LCC = table.Column<string>(nullable: true),
                    DenormPrimaryAuthor = table.Column<string>(nullable: true),
                    DenormDownloadDate = table.Column<long>(nullable: false),
                    ReviewId = table.Column<int>(nullable: true),
                    NotesId = table.Column<int>(nullable: true),
                    DownloadDataId = table.Column<int>(nullable: true),
                    NavigationDataId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Books", x => x.BookId);
                    table.ForeignKey(
                        name: "FK_Books_DownloadData_DownloadDataId",
                        column: x => x.DownloadDataId,
                        principalTable: "DownloadData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Books_BookNavigationData_NavigationDataId",
                        column: x => x.NavigationDataId,
                        principalTable: "BookNavigationData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Books_BookNotes_NotesId",
                        column: x => x.NotesId,
                        principalTable: "BookNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Books_UserReview_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "UserReview",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FilenameAndFormatData",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileName = table.Column<string>(nullable: true),
                    FileType = table.Column<string>(nullable: true),
                    LastModified = table.Column<string>(nullable: true),
                    BookId = table.Column<string>(nullable: true),
                    Extent = table.Column<int>(nullable: false),
                    MimeType = table.Column<string>(nullable: true),
                    BookDataBookId = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FilenameAndFormatData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FilenameAndFormatData_Books_BookDataBookId",
                        column: x => x.BookDataBookId,
                        principalTable: "Books",
                        principalColumn: "BookId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Person",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(nullable: true),
                    Aliases = table.Column<string>(nullable: true),
                    PersonType = table.Column<int>(nullable: false),
                    BirthDate = table.Column<int>(nullable: false),
                    DeathDate = table.Column<int>(nullable: false),
                    Webpage = table.Column<string>(nullable: true),
                    BookDataBookId = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Person", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Person_Books_BookDataBookId",
                        column: x => x.BookDataBookId,
                        principalTable: "Books",
                        principalColumn: "BookId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Books_DownloadDataId",
                table: "Books",
                column: "DownloadDataId");

            migrationBuilder.CreateIndex(
                name: "IX_Books_NavigationDataId",
                table: "Books",
                column: "NavigationDataId");

            migrationBuilder.CreateIndex(
                name: "IX_Books_NotesId",
                table: "Books",
                column: "NotesId");

            migrationBuilder.CreateIndex(
                name: "IX_Books_ReviewId",
                table: "Books",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_FilenameAndFormatData_BookDataBookId",
                table: "FilenameAndFormatData",
                column: "BookDataBookId");

            migrationBuilder.CreateIndex(
                name: "IX_Person_BookDataBookId",
                table: "Person",
                column: "BookDataBookId");

            migrationBuilder.CreateIndex(
                name: "IX_UserNote_BookNotesId",
                table: "UserNote",
                column: "BookNotesId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FilenameAndFormatData");

            migrationBuilder.DropTable(
                name: "Person");

            migrationBuilder.DropTable(
                name: "UserNote");

            migrationBuilder.DropTable(
                name: "Books");

            migrationBuilder.DropTable(
                name: "DownloadData");

            migrationBuilder.DropTable(
                name: "BookNavigationData");

            migrationBuilder.DropTable(
                name: "BookNotes");

            migrationBuilder.DropTable(
                name: "UserReview");
        }
    }
}
