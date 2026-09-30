using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudiesFinal.Models.Entities;
using StudiesFinal.Web.Interface;
using StudiesFinal.Web.Models;
using StudiesFinal.Web.Models.ViewModels;
using System.Diagnostics;

namespace StudiesFinal.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly IEntityRepository<Study, int> _studyRepository;
        private readonly IEntityRepository<Patient, int> _patientRepository;

        public HomeController(IEntityRepository<Study, int> studyRepository, IEntityRepository<Patient, int> patientRepository)
        {
            _studyRepository = studyRepository;
            _patientRepository = patientRepository;
        }

        public async Task<IActionResult> Index()
        {
            var studies = await _studyRepository.GetAll();
            var byStatus = await studies
                .GroupBy(s => s.Status)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToListAsync();

            var today = DateTime.Today;
            var counts = new StudyCounts
            {
                InProgress = byStatus.FirstOrDefault(x => x.Key == StudyStatus.InProgress)?.Count ?? 0,
                ToSign = byStatus.FirstOrDefault(x => x.Key == StudyStatus.ToSign)?.Count ?? 0,
                Completed = byStatus.FirstOrDefault(x => x.Key == StudyStatus.Completed)?.Count ?? 0,
                Today = await studies.CountAsync(s => s.StudyDate >= today && s.StudyDate < today.AddDays(1)),
                Patients = await (await _patientRepository.GetAll()).CountAsync()
            };

            // Lo último que se ha movido (para retomar el trabajo)
            var recent = await (await _studyRepository.GetAll(includeProperties: "Patient,SignedBy"))
                .Where(s => s.Status != StudyStatus.Completed)
                .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
                .ThenByDescending(s => s.Id)
                .Take(10)
                .ToListAsync();

            ViewBag.Recent = recent.Select(s => { var vm = new StudyDisplayVM(); vm.Import(s); return vm; }).ToList();

            return View(counts);
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
