namespace MyKniga.Web.Controllers
{
    using System.Linq;
    using System.Threading.Tasks;
    using AutoMapper;
    using Common;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Models;
    using Services.Interfaces;
    using Services.Models.Book;

    public class BooksController : BaseController
    {
        private readonly IBooksService booksService;
        private readonly ITagsService tagsService;
        private readonly IUsersService usersService;

        public BooksController(IBooksService booksService, ITagsService tagsService, IUsersService usersService)
        {
            this.booksService = booksService;
            this.tagsService = tagsService;
            this.usersService = usersService;
        }

        [Authorize(Policy = GlobalConstants.AdministratorOrPublisherPolicyName)]
        public IActionResult Create()
        {
            return this.View();
        }

        [HttpPost]
        [Authorize(Policy = GlobalConstants.AdministratorOrPublisherPolicyName)]
        public async Task<IActionResult> Create(BookCreateBindingModel model)
        {
            if (!this.ModelState.IsValid)
            {
                this.ShowErrorMessage(NotificationMessages.BookCreateErrorMessage);
                return this.View(model);
            }

            var publisherId = await this.usersService.GetPublisherIdByUserNameAsync(this.User.Identity.Name);

            if (publisherId == null)
            {
                this.ShowErrorMessage(NotificationMessages.BookCreateErrorMessage);
                return this.RedirectToAction("Index", "Home");
            }

            var serviceBook = Mapper.Map<BookCreateServiceModel>(model);

            serviceBook.PublisherId = publisherId;

            var bookId = await this.booksService.CreateBookAsync(serviceBook);

            if (bookId == null)
            {
                this.ShowErrorMessage(NotificationMessages.BookCreateErrorMessage);
                return this.View(model);
            }

            this.ShowSuccessMessage(NotificationMessages.BookCreateSuccessMessage);
            return this.RedirectToAction("Details", "Books", new {id = bookId});
        }

        public IActionResult Index()
        {
            return this.View();
        }

        public async Task<IActionResult> GetBooks()
        {
            var allBooks = await this.booksService.GetAllBooksAsync<BookListingServiceModel>();

            return this.Ok(allBooks);
        }

        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return this.NotFound();
            }

            var book = await this.booksService.GetBookByIdAsync<BookDetailsServiceModel>(id);

            if (book == null)
            {
                return this.NotFound();
            }

            var viewBook = Mapper.Map<BookDetailsViewModel>(book);

            viewBook.CanEdit = await this.UserCanEditBookAsync(book);

            if (viewBook.CanEdit)
            {
                var allTags = (await this.tagsService.GetAllTagsAsync())
                    .Select(Mapper.Map<TagDisplayViewModel>)
                    .ToArray();

                viewBook.AllTags = allTags;
            }

            return this.View(viewBook);
        }

        [HttpPost]
        [Authorize(Policy = GlobalConstants.AdministratorOrPublisherPolicyName)]
        public async Task<IActionResult> AddTagToBook(string bookId, string tagId)
        {
            if (bookId == null || tagId == null)
            {
                return this.Ok(new {success = false});
            }

            var book = await this.booksService.GetBookByIdAsync<BookDetailsServiceModel>(bookId);

            if (book == null || !await this.UserCanEditBookAsync(book))
            {
                return this.Ok(new {success = false});
            }

            var isSuccess = await this.booksService.AddTagToBookAsync(bookId, tagId);

            return this.Ok(new {success = isSuccess});
        }

        [HttpPost]
        [Authorize(Policy = GlobalConstants.AdministratorOrPublisherPolicyName)]
        public async Task<IActionResult> RemoveTagFromBook(string bookId, string tagId)
        {
            if (bookId == null || tagId == null)
            {
                return this.Ok(new {success = false});
            }

            var book = await this.booksService.GetBookByIdAsync<BookDetailsServiceModel>(bookId);

            if (book == null || !await this.UserCanEditBookAsync(book))
            {
                return this.Ok(new {success = false});
            }

            await this.booksService.RemoveTagFromBookAsync(bookId, tagId);

            return this.Ok(new {success = true});
        }

        private async Task<bool> UserCanEditBookAsync(BookDetailsServiceModel model)
        {
            if (!this.User.Identity.IsAuthenticated)
            {
                return false;
            }

            if (this.User.IsInRole(GlobalConstants.AdministratorRoleName))
            {
                return true;
            }

            var publisherId = await this.usersService.GetPublisherIdByUserNameAsync(this.User.Identity.Name);

            return publisherId == model.PublisherId;
        }
    }
}