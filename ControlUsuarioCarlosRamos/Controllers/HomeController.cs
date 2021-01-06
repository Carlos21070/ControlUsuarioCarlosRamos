using ControlUsuarioCarlosRamos.Helpers;
using ControlUsuarioCarlosRamos.Models;
using ControlUsuarioCarlosRamos.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ControlUsuarioCarlosRamos.Controllers
{
    public class HomeController: Controller
    {
        public IWebHostEnvironment Environment { get; set; }
        public HomeController(IWebHostEnvironment env)
        {
            Environment = env;
        }

        [Authorize(Roles = "Cliente")]
        public IActionResult Index()
        {
            return View();
        }
        [AllowAnonymous]
        public IActionResult Registrar()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public IActionResult Registrar(Usuario us, string contrasena, string contrasena2)
        {
            ControlUsuariosContext context = new ControlUsuariosContext();
            try
            {
                UsuarioRepository<Usuario> reposUsuario = new UsuarioRepository<Usuario>(context);
                if (context.Usuario.Any(x => x.Correo == us.Correo))
                {
                    ModelState.AddModelError("", "Este usuario ya forma parte de UltimateFut.");
                    return View(us);
                }
                else
                {
                    if (contrasena == contrasena2)
                    {
                        us.Contrasena = HashingHelpers.GetHelper(contrasena);
                        us.Codigo = CodeHelper.GetCodigo();
                        us.Activo = 0;
                        reposUsuario.Insert(us);

                        MailMessage mensajeUt = new MailMessage();
                        mensajeUt.From = new MailAddress("sistemascomputacionales7g@gmail.com", "UltimateFut");
                        mensajeUt.To.Add(us.Correo);
                        mensajeUt.Subject = "Verifica tu correo para UltimateFut";
                        string text = System.IO.File.ReadAllText(Environment.WebRootPath + "/ConfirmacionDeCorreo.html");
                        mensajeUt.Body = text.Replace("{##codigo##}", us.Codigo.ToString());
                        mensajeUt.IsBodyHtml = true;

                        SmtpClient clienteUt = new SmtpClient("smtp.gmail.com", 587);
                        clienteUt.EnableSsl = true;
                        clienteUt.UseDefaultCredentials = false;
                        clienteUt.Credentials = new NetworkCredential("sistemascomputacionales7g@gmail.com", "sistemas7g");
                        clienteUt.Send(mensajeUt);
                        return RedirectToAction("Activar");
                    }
                    else
                    {
                        ModelState.AddModelError("", "Ambas contraseñas no coinciden entre sí, intentalo de nuevo.");
                        return View(us);
                    }
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(us);
            }
        }
        [AllowAnonymous]
        public IActionResult IniciarSesion()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> IniciarSesion(Usuario us, bool mantener)
        {
            ControlUsuariosContext context = new ControlUsuariosContext();
            UsuarioRepository<Usuario> reposUsuario = new UsuarioRepository<Usuario>(context);
            var datos = reposUsuario.GetUserByEmail(us.Correo);
            if (datos != null && HashingHelpers.GetHelper(us.Contrasena) == datos.Contrasena)
            {
                if (datos.Activo == 1)
                {
                    List<Claim> info = new List<Claim>();
                    info.Add(new Claim(ClaimTypes.Name, "Usuario" + datos.Nombre));
                    info.Add(new Claim(ClaimTypes.Role, "Cliente"));
                    info.Add(new Claim("Correo", datos.Correo));
                    info.Add(new Claim("Nombre", datos.Nombre));

                    var claimsidentity = new ClaimsIdentity(info, CookieAuthenticationDefaults.AuthenticationScheme);
                    var claimsprincipal = new ClaimsPrincipal(claimsidentity);

                    if (mantener == true)
                    {
                        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsprincipal,
                            new AuthenticationProperties { IsPersistent = true });
                    }
                    else
                    {
                        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsprincipal,
                            new AuthenticationProperties { IsPersistent = false });
                    }
                    return RedirectToAction("Index");
                }
                else
                {
                    ModelState.AddModelError("", "Activa tu cuenta para poder iniciar sesión en UTfut.");
                    return View(us);
                }
            }
            else
            {
                ModelState.AddModelError("", "El correo electrónico o la contraseña son incorrectas.");
                return View(us);
            }
        }

        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> CerrarSesion()
        {
            await HttpContext.SignOutAsync();
            return RedirectToAction("IniciarSesion");
        }


        [AllowAnonymous]
        public IActionResult Activar()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public IActionResult Activar(int codigo)
        {
            ControlUsuariosContext context = new ControlUsuariosContext();
            UsuarioRepository<Usuario> reposUsuario = new UsuarioRepository<Usuario>(context);
            var usuario = context.Usuario.FirstOrDefault(x => x.Codigo == codigo);

            if (usuario != null && usuario.Activo == 0)
            {
                var cgo = usuario.Codigo;
                if (codigo == cgo)
                {
                    usuario.Activo = 1;
                    reposUsuario.Edit(usuario);
                    return RedirectToAction("IniciarSesion");
                }
                else
                {
                    ModelState.AddModelError("", "Tu codigo para UTfut no es correcto, intentalo de nuevo.");
                    return View((object)codigo);
                }
            }
            else
            {
                ModelState.AddModelError("", "El usuario no existe en nuestra plataforma.");
                return View((object)codigo);
            }
        }

        [Authorize(Roles = "Cliente")]
        public IActionResult CambiarContra()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Cliente")]
        public IActionResult CambiarContra(string correo, string contra, string nuevaContrasena, string nvaContrasena)
        {
            ControlUsuariosContext context = new ControlUsuariosContext();

            try
            {
                UsuarioRepository<Usuario> reposUsuario = new UsuarioRepository<Usuario>(context);
                var usuario = reposUsuario.GetUserByEmail(correo);

                if (usuario.Contrasena != HashingHelpers.GetHelper(contra))
                {
                    ModelState.AddModelError("", "La contraseña UTfut es incorrecta.");
                    return View();
                }
                else
                {
                    if (nuevaContrasena != nvaContrasena)
                    {
                        ModelState.AddModelError("", "Ambas contraseñas no coinciden entre sí, intentelo de nuevo.");
                        return View();
                    }
                    else if (usuario.Contrasena == HashingHelpers.GetHelper(nuevaContrasena))
                    {
                        ModelState.AddModelError("", "Esta introduciendo una contraseña antigua, intentelo una cez mas con una contaseña distinta.");
                        return View();
                    }
                    else
                    {
                        usuario.Contrasena = HashingHelpers.GetHelper(nuevaContrasena);
                        reposUsuario.Edit(usuario);
                        return RedirectToAction("IniciarSesion");
                    }
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View();
            }
        }

        [AllowAnonymous]
        public IActionResult RecuperarContra()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public IActionResult RecuperarContra(string correo)
        {
            try
            {
                ControlUsuariosContext context = new ControlUsuariosContext();
                UsuarioRepository<Usuario> repository = new UsuarioRepository<Usuario>(context);
                var usuario = repository.GetUserByEmail(correo);

                if (usuario != null)
                {
                    var contraTemp = CodeHelper.GetCodigo();
                    MailMessage mensaje = new MailMessage();
                    mensaje.From = new MailAddress("sistemascomputacionales7g@gmail.com", "UltimateFut");
                    mensaje.To.Add(correo);
                    mensaje.Subject = "Recupera tu contraseña en UTfut";
                    string text = System.IO.File.ReadAllText(Environment.WebRootPath + "/RecuperacionDeContrasena.html");
                    mensaje.Body = text.Replace("{##contraTemp##}", contraTemp.ToString());
                    mensaje.IsBodyHtml = true;

                    SmtpClient cliente = new SmtpClient("smtp.gmail.com", 587);
                    cliente.EnableSsl = true;
                    cliente.UseDefaultCredentials = false;
                    cliente.Credentials = new NetworkCredential("sistemascomputacionales7g@gmail.com", "sistemas7g");
                    cliente.Send(mensaje);
                    usuario.Contrasena = HashingHelpers.GetHelper(contraTemp.ToString());
                    repository.Edit(usuario);
                    return RedirectToAction("IniciarSesion");
                }
                else
                {
                    ModelState.AddModelError("", "El correo UltimateFut no se encuentra registrado en nuestra página.");
                    return View();
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View((object)correo);
            }
        }

        [Authorize(Roles = "Cliente")]
        public IActionResult EliminarCuenta()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Cliente")]
        public IActionResult EliminarCuenta(string correo, string contra)
        {
            ControlUsuariosContext context = new ControlUsuariosContext();
            try
            {
                UsuarioRepository<Usuario> reposUsuario = new UsuarioRepository<Usuario>(context);
                var usuario = reposUsuario.GetUserByEmail(correo);
                if (usuario != null)
                {
                    if (HashingHelpers.GetHelper(contra) == usuario.Contrasena)
                    {
                        reposUsuario.Delete(usuario);
                    }
                    else
                    {
                        ModelState.AddModelError("", "La contraseña UTfut introducida es incorrecta, intentelo de nuevo.");
                        return View();
                    }
                }
                return RedirectToAction("IniciarSesion");
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "Ocurrió un error. Inténtelo de nuevo en otro momento.");
                return View();
            }
        }

        [AllowAnonymous]
        public IActionResult Denegado()
        {
            return View();
        }
    }
}
