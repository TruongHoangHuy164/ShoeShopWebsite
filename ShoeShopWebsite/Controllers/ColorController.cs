using Microsoft.AspNetCore.Mvc;
using ShoeShopWebsite.Models;
using System.Linq;

namespace ShoeShopWebsite.Controllers
{
    public class ColorController : Controller
    {
        private readonly NikeShopDbContext _context; // Replace with your actual DbContext

        public ColorController(NikeShopDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var colors = _context.Colors.ToList();
            return View(colors);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Color color)
        {
            if (ModelState.IsValid)
            {
                _context.Colors.Add(color);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View(color);
        }

        public IActionResult Edit(int id)
        {
            var color = _context.Colors.Find(id);
            if (color == null)
            {
                return NotFound();
            }
            return View(color);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Color color)
        {
            if (id != color.ColorID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                _context.Update(color);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View(color);
        }

        public IActionResult Delete(int id)
        {
            var color = _context.Colors.Find(id);
            if (color == null)
            {
                return NotFound();
            }
            return View(color);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var color = _context.Colors.Find(id);
            _context.Colors.Remove(color);
            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
        }
    }
}