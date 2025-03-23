using Microsoft.AspNetCore.Mvc;
using ShoeShopWebsite.Models;
using System.Linq;

namespace ShoeShopWebsite.Controllers
{
    public class SizeController : Controller
    {
        private readonly NikeShopDbContext _context; // Replace with your actual DbContext

        public SizeController(NikeShopDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var sizes = _context.Sizes.ToList();
            return View(sizes);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Size size)
        {
            if (ModelState.IsValid)
            {
                _context.Sizes.Add(size);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View(size);
        }

        public IActionResult Edit(int id)
        {
            var size = _context.Sizes.Find(id);
            if (size == null)
            {
                return NotFound();
            }
            return View(size);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Size size)
        {
            if (id != size.SizeID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                _context.Update(size);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View(size);
        }

        public IActionResult Delete(int id)
        {
            var size = _context.Sizes.Find(id);
            if (size == null)
            {
                return NotFound();
            }
            return View(size);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var size = _context.Sizes.Find(id);
            _context.Sizes.Remove(size);
            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
        }
    }
}