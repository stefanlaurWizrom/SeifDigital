using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using SeifDigital.Models;
using SeifDigital.Data;
using SeifDigital.Services;

namespace SeifDigital.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _audit;
        private readonly EncryptionService _crypto;

        public HomeController(ApplicationDbContext context, EncryptionService crypto, AuditService audit)
        {
            _context = context;
            _crypto = crypto;
            _audit = audit;
        }

        // Pagina principală (nu trimite parole în HTML)
        [HttpGet]
        public IActionResult Index()
        {
            // Verificare 2FA
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
            {
                return RedirectToAction("Verificare2FA", "Auth");
            }

            // Luăm DOAR datele userului curent
            string userCurent = User.Identity?.Name ?? "User_Domeniu";

            var listaDate = _context.InformatiiSensibile
                .Where(x => x.NumeUtilizator == userCurent)
                .ToList();

            return View(listaDate);
        }

        // Salvare date (parola se salvează criptată)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SalveazaDateSensibile(string titlu, string usernameSalvat, string parolaClara)
        {
            // Verificare 2FA
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
            {
                // audit: userul încearcă să creeze fără 2FA valid
                _audit.Log(HttpContext,
                    eventType: "Secret.Create",
                    outcome: "Fail",
                    reason: "2FA_NotValidated",
                    targetType: "Secret",
                    targetId: null,
                    details: new { titlu });

                return RedirectToAction("Verificare2FA", "Auth");
            }

            if (string.IsNullOrWhiteSpace(titlu) ||
                string.IsNullOrWhiteSpace(usernameSalvat) ||
                string.IsNullOrWhiteSpace(parolaClara))
            {
                _audit.Log(HttpContext,
                    eventType: "Secret.Create",
                    outcome: "Fail",
                    reason: "Validation",
                    targetType: "Secret",
                    targetId: null,
                    details: new { titlu, usernameSalvat });

                return RedirectToAction("Index");
            }

            // 1) Criptăm parola (cu cheia din appsettings.json -> Crypto:MasterKeyBase64)
            string parolaCriptata = _crypto.Encrypt(parolaClara);

            // 2) Construim rândul pentru SQL
            var nou = new InformatieSensibila
            {
                NumeUtilizator = User.Identity?.Name ?? "User_Domeniu",
                TitluAplicatie = titlu,
                UsernameSalvat = usernameSalvat,
                DateCriptate = parolaCriptata
            };

            // 3) Salvăm în SQL
            _context.InformatiiSensibile.Add(nou);
            _context.SaveChanges();

            // audit success (NU logăm parola)
            _audit.Log(HttpContext,
                eventType: "Secret.Create",
                outcome: "Success",
                targetType: "Secret",
                targetId: nou.Id.ToString(),
                details: new { titlu, usernameSalvat });

            return RedirectToAction("Index");
        }

        // ====== AJAX: returnează parola decriptată ca JSON (nu intră în HTML la refresh) ======
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GetParolaJson(int id)
        {
            // 1) Verificare 2FA pentru apelul AJAX
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
            {
                _audit.Log(HttpContext,
                    eventType: "Secret.ViewPassword",
                    outcome: "Fail",
                    reason: "2FA_NotValidated",
                    targetType: "Secret",
                    targetId: id.ToString());

                return Unauthorized(new { ok = false, message = "2FA nu este validat." });
            }

            // 2) User curent
            string userCurent = User.Identity?.Name ?? "User_Domeniu";

            // 3) Luăm doar înregistrarea userului curent
            var item = _context.InformatiiSensibile
                .FirstOrDefault(x => x.Id == id && x.NumeUtilizator == userCurent);

            if (item == null)
            {
                _audit.Log(HttpContext,
                    eventType: "Secret.ViewPassword",
                    outcome: "Fail",
                    reason: "NotFoundOrNotOwner",
                    targetType: "Secret",
                    targetId: id.ToString());

                return NotFound(new { ok = false, message = "Nu găsesc înregistrarea." });
            }

            // 4) Decriptăm și returnăm JSON
            try
            {
                string parolaClara = _crypto.Decrypt(item.DateCriptate ?? "");

                // audit success (NU logăm parola)
                _audit.Log(HttpContext,
                    eventType: "Secret.ViewPassword",
                    outcome: "Success",
                    targetType: "Secret",
                    targetId: id.ToString(),
                    details: new { titlu = item.TitluAplicatie });

                return Json(new { ok = true, parola = parolaClara });
            }
            catch
            {
                _audit.Log(HttpContext,
                    eventType: "Secret.ViewPassword",
                    outcome: "Fail",
                    reason: "DecryptError",
                    targetType: "Secret",
                    targetId: id.ToString(),
                    details: new { titlu = item.TitluAplicatie });

                // Dacă ai date vechi criptate cu altă cheie/metodă, aici poate pica.
                return BadRequest(new { ok = false, message = "Nu pot decripta această înregistrare (cheie/metodă diferită sau date corupte)." });
            }
        }

        // ====== ȘTERGERE: șterge doar înregistrarea userului curent ======
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Sterge(int id)
        {
            // Verificare 2FA
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
            {
                _audit.Log(HttpContext,
                    eventType: "Secret.Delete",
                    outcome: "Fail",
                    reason: "2FA_NotValidated",
                    targetType: "Secret",
                    targetId: id.ToString());

                return RedirectToAction("Verificare2FA", "Auth");
            }

            string userCurent = User.Identity?.Name ?? "User_Domeniu";

            var item = _context.InformatiiSensibile
                .FirstOrDefault(x => x.Id == id && x.NumeUtilizator == userCurent);

            if (item == null)
            {
                _audit.Log(HttpContext,
                    eventType: "Secret.Delete",
                    outcome: "Fail",
                    reason: "NotFoundOrNotOwner",
                    targetType: "Secret",
                    targetId: id.ToString());

                return RedirectToAction("Index");
            }

            var titlu = item.TitluAplicatie;

            _context.InformatiiSensibile.Remove(item);
            _context.SaveChanges();

            _audit.Log(HttpContext,
                eventType: "Secret.Delete",
                outcome: "Success",
                targetType: "Secret",
                targetId: id.ToString(),
                details: new { titlu });

            return RedirectToAction("Index");
        }
    }
}
