﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using NuGet.Common;
using WebApplication3.Models;


namespace WebApplication3.Controllers{

    public class UsersController : Controller
    {
        private ApplicationDbContext _context;
        private IWebHostEnvironment _environment;
        private UserManager<User> _userManager;
        private IEmailSender _emailSender;
        private SignInManager<User> _signInManager;
        private ILogger<UsersController> _logger;
       
        public UsersController(IWebHostEnvironment environment, UserManager<User> userManager,IEmailSender emailSender, SignInManager<User> signInManager,ApplicationDbContext context,ILogger<UsersController> logger)
        {
            _context = context;
            _environment = environment;
            _userManager = userManager;
            _emailSender = emailSender;
            _signInManager = signInManager;
            _logger = logger;
        }

        public ActionResult Create()
        {
            if (_signInManager.IsSignedIn(User))
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AddSellerViewModel addSellerViewModel)
        {
            var files = HttpContext.Request.Form.Files;
            var returnUrl = Url.Content("~/");
            if (ModelState.IsValid)
            {
                string uploadFolder = "";
                string uniqueFileName = "";
                string filePath = "";
                try
                {
                    if (files[0].Length > 0)
                    {

                        uploadFolder = Path.Combine(_environment.WebRootPath, "img");
                        uniqueFileName = Guid.NewGuid().ToString() + "_" +
                                         files[0].FileName.Replace(' ', '_');
                        filePath = Path.Combine(uploadFolder, uniqueFileName);
                        files[0].CopyTo(new FileStream(filePath, FileMode.Create));
                    }
                }
                catch (Exception e)
                {
                    uniqueFileName = "defaultimg.png";
                }


                var user = new User()
                {
                    UserName = addSellerViewModel.Email, Email = addSellerViewModel.Email, image = uniqueFileName,
                    Street = addSellerViewModel.Street,
                    PhoneNumber = addSellerViewModel.PhoneNumber, City = addSellerViewModel.City,
                    Province = addSellerViewModel.Province,
                    Firstname = addSellerViewModel.Firstname, Lastname = addSellerViewModel.Lastname, ApprovalStatus = 0
                };
                var result = await _userManager.CreateAsync(user, addSellerViewModel.Password);
                
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Seller");
                    var userId = user.Id;
                    string folderPath = "";
                    string newFileName = "";
                    string pathofFile = "";
                  
                        if (files[1].Length > 0)
                        {

                            folderPath = Path.Combine(_environment.WebRootPath, "UserDocument/images");
                            newFileName = Guid.NewGuid().ToString() + "_" +
                                          files[1].FileName.Replace(' ', '_');
                            pathofFile = Path.Combine(folderPath, newFileName);
                            files[1].CopyTo(new FileStream(pathofFile, FileMode.Create));
                        }
                    var imageSellerVerify = new ImageSellerVerify()
                    {
                        ImagePath = newFileName,
                        SellerId = userId
                    };
                    _context.ImageSellerVerifies.Add(imageSellerVerify);
                    _context.SaveChangesAsync();
                    _logger.LogInformation("User created a new account with password");

                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = user.Id, code = code, returnUrl = returnUrl },
                        protocol: Request.Scheme);

                    await _emailSender.SendEmailAsync(addSellerViewModel.Email, "Confirm your email",
                        $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage("RegisterConfirmation", new { email = addSellerViewModel.Email, returnUrl = returnUrl });
                    } 
                    else
                    {
                        CookieOptions option = new CookieOptions();
                        option.Expires = DateTime.Now.AddSeconds(10);
                        Response.Cookies.Append("SellerRegister", "true", option);
                        return LocalRedirect(returnUrl);
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            return View(addSellerViewModel);
        }
        [Authorize(Roles = "Admin")]
        public IActionResult UsersView()
        {
            ViewBag.Admins = _userManager.GetUsersInRoleAsync("Admin").Result;
            ViewBag.Buyers = _userManager.GetUsersInRoleAsync("Buyer").Result;
            ViewBag.Seller = _userManager.GetUsersInRoleAsync("Seller").Result;
            return View();
         }

        [Authorize(Roles = "Admin")]
        public IActionResult UsersUnApproved()
        {
            var users = _context.Users.Where(u => u.ApprovalStatus != 1).ToList();
            return View(users);
        }
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ApproveUser(string id)
        {
            var user = _context.Users.SingleOrDefault(u=>u.Id== id);
            user.ApprovalStatus = 1;
            _context.Update(user);
            
            await _context.SaveChangesAsync();
            CookieOptions option = new CookieOptions();
            option.Expires = DateTime.Now.AddSeconds(10);
            Response.Cookies.Append("UserApprove", "true", option);
            return RedirectToAction(nameof(UsersView));
        }
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = _context.Users.SingleOrDefault(u=>u.Id== id);
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            CookieOptions option = new CookieOptions();
            option.Expires = DateTime.Now.AddSeconds(10);
            Response.Cookies.Append("UserRemove", "true", option);
            return RedirectToAction(nameof(UsersView));
        }
        [Authorize(Roles = "Admin")]
        public  IActionResult UserProfile(string id)
        {
            ViewBag.user = _context.Users.SingleOrDefault(u => u.Id == id);
            ViewBag.Document = _context.ImageSellerVerifies.SingleOrDefault(u => u.SellerId == id);
            
            return View();
        }
    }
}