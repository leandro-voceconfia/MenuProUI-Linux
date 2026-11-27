using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using MenuProUI.Models;

namespace MenuProUI.Services;

/// <summary>
/// Serviço responsável por abrir/conectar acessos de diferentes tipos.
/// Detecta tipo de acesso e executa comando apropriado:
/// - SSH: abre conexão SSH no terminal
/// - RDP: abre o cliente RDP nativo do sistema
/// - URL: abre navegador padrão
/// - MTK: abre WinBox (quando disponível) ou protocolo winbox://
/// </summary>
public static class AccessLauncher
{
    /// <summary>
    /// Abre um acesso baseado em seu tipo (SSH, RDP, URL ou MTK).
    /// Detecta automaticamente e executa a ação apropriada.
    /// </summary>
    /// <param name="e">Acesso a ser aberto</param>
    public static void Open(AccessEntry e)
    {
        switch (e.Tipo)
        {
            case AccessType.URL:
                OpenUrl(e.Url);
                break;
            case AccessType.SSH:
                OpenSsh(e);
                break;
            case AccessType.RDP:
                // Inicia o cliente RDP do sistema sem bloquear a UI.
                OpenRdpInTerminal(e);
                break;
            case AccessType.MTK:
                OpenMtk(e);
                break;
        }
    }

    /// <summary>
    /// Abre RDP com suporte a senha via stdin.
    /// Útil para automatizar conexões RDP com credenciais no Linux.
    /// No Windows, a senha é solicitada pelo cliente RDP nativo.
    /// </summary>
    /// <param name="e">Acesso RDP com configurações</param>
    /// <param name="password">Senha para autenticação RDP</param>
    public static void OpenRdpWithPassword(AccessEntry e, string password)
    {
        if (OperatingSystem.IsWindows())
            throw new NotSupportedException("O cliente RDP do Windows não aceita senha pela entrada padrão. A senha será solicitada pelo próprio cliente.");

        if (string.IsNullOrWhiteSpace(e.Host)) return;

        var client = FindRdpClient();
        if (client is null) return;

        // Usa porta padrão 3389 se não especificada
        var port = (e.Porta is > 0) ? e.Porta.Value : 3389;

        // Configura processo para executar cliente RDP
        var psi = new ProcessStartInfo
        {
            FileName = client,
            UseShellExecute = false,
            RedirectStandardInput = true,  // Entrada padrão para senha
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        // Monta argumentos do xfreerdp
        psi.ArgumentList.Add($"/v:{e.Host}:{port}");

        if (!string.IsNullOrWhiteSpace(e.Usuario))
            psi.ArgumentList.Add($"/u:{e.Usuario}");

        if (!string.IsNullOrWhiteSpace(e.Dominio))
            psi.ArgumentList.Add($"/d:{e.Dominio}");

        if (e.RdpIgnoreCert)
            psi.ArgumentList.Add("/cert:ignore");

        // Force credentials from stdin (evita congelar)
        psi.ArgumentList.Add("/from-stdin:force");

        // Configura tela/resolução
        if (e.RdpFullScreen)
        {
            psi.ArgumentList.Add("/f");
        }
        else if (e.RdpDynamicResolution)
        {
            // xfreerdp3 usa sintaxe diferente de xfreerdp2
            psi.ArgumentList.Add(client.EndsWith("3", StringComparison.Ordinal) ? "+dynamic-resolution" : "/dynamic-resolution");
        }
        else if (e.RdpWidth is > 0 && e.RdpHeight is > 0)
        {
            psi.ArgumentList.Add($"/size:{e.RdpWidth}x{e.RdpHeight}");
        }

        var p = Process.Start(psi);
        if (p is null) return;

        // Drena saída padrão/erro assincronamente (evita deadlock)
        _ = p.StandardOutput.ReadToEndAsync();
        _ = p.StandardError.ReadToEndAsync();

        // Envia senha via stdin
        using (var sw = p.StandardInput)
        {
            sw.WriteLine(password);
            sw.Flush();
        }
    }

    /// <summary>
    /// Abre conexão RDP usando o cliente apropriado ao sistema.
    /// </summary>
    /// <param name="e">Acesso RDP com configurações</param>
    public static void OpenRdpInTerminal(AccessEntry e)
    {
        if (string.IsNullOrWhiteSpace(e.Host)) return;

        var port = (e.Porta is > 0) ? e.Porta.Value : 3389;

        if (OperatingSystem.IsWindows())
        {
            OpenWindowsRdp(e, port);
            return;
        }

        var client = FindRdpClient() ?? "xfreerdp";

        // Monta comando RDP (sem /p = cliente pede senha no terminal)
        var cmd = $"{client} /v:{e.Host}:{port}";

        if (!string.IsNullOrWhiteSpace(e.Usuario))
            cmd += $" /u:{EscapeShell(e.Usuario)}";

        if (!string.IsNullOrWhiteSpace(e.Dominio))
            cmd += $" /d:{EscapeShell(e.Dominio)}";

        if (e.RdpIgnoreCert)
            cmd += " /cert:ignore";

        // Configurações de tela
        if (e.RdpFullScreen)
            cmd += " /f";
        else if (e.RdpDynamicResolution)
            cmd += (client.EndsWith("3", StringComparison.Ordinal) ? " +dynamic-resolution" : " /dynamic-resolution");
        else if (e.RdpWidth is > 0 && e.RdpHeight is > 0)
            cmd += $" /size:{e.RdpWidth}x{e.RdpHeight}";

        // Abre em terminal
        OpenTerminal(cmd);
    }

    /// <summary>
    /// Localiza cliente RDP instalado no sistema.
    /// Tenta encontrar xfreerdp ou xfreerdp3.
    /// </summary>
    /// <returns>Caminho do cliente RDP ou null se não encontrado</returns>
    private static string? FindRdpClient()
    {
        if (IsOnPath("xfreerdp")) return "xfreerdp";
        if (IsOnPath("xfreerdp3")) return "xfreerdp3";
        return null;
    }

    private static void OpenWindowsRdp(AccessEntry e, int port)
    {
        var host = e.Host!.Trim();
        EnsureSingleLine(host, nameof(e.Host));
        if (host.Contains(':') && !host.StartsWith('['))
            host = $"[{host}]";

        var settings = new[]
        {
            $"full address:s:{host}:{port}",
            "prompt for credentials:i:1",
            $"screen mode id:i:{(e.RdpFullScreen ? 2 : 1)}",
            "use multimon:i:0"
        }.ToList();

        if (!string.IsNullOrWhiteSpace(e.Usuario))
        {
            EnsureSingleLine(e.Usuario, nameof(e.Usuario));
            if (!string.IsNullOrWhiteSpace(e.Dominio))
            {
                EnsureSingleLine(e.Dominio, nameof(e.Dominio));
                settings.Add($"username:s:{e.Dominio}\\{e.Usuario}");
            }
            else
            {
                settings.Add($"username:s:{e.Usuario}");
            }
        }

        if (e.RdpFullScreen)
        {
            settings.Add("dynamic resolution:i:1");
        }
        else if (e.RdpDynamicResolution)
        {
            settings.Add("dynamic resolution:i:1");
        }
        else if (e.RdpWidth is > 0 && e.RdpHeight is > 0)
        {
            settings.Add($"desktopwidth:i:{e.RdpWidth.Value}");
            settings.Add($"desktopheight:i:{e.RdpHeight.Value}");
        }

        var rdpFile = Path.Combine(Path.GetTempPath(), $"MenuProUI-{Guid.NewGuid():N}.rdp");
        File.WriteAllText(rdpFile, string.Join(Environment.NewLine, settings), Encoding.Unicode);

        var psi = new ProcessStartInfo
        {
            FileName = "mstsc.exe",
            UseShellExecute = true
        };
        psi.ArgumentList.Add(rdpFile);
        Process.Start(psi);
    }

    private static void EnsureSingleLine(string value, string fieldName)
    {
        if (value.Contains('\r') || value.Contains('\n'))
            throw new ArgumentException("O valor não pode conter quebras de linha.", fieldName);
    }

    /// <summary>
    /// Verifica se um executável está disponível no PATH.
    /// </summary>
    /// <param name="exe">Nome do executável a buscar</param>
    /// <returns>true se encontrado, false caso contrário</returns>
    private static bool IsOnPath(string exe)
    {
        return FindExecutable(exe) is not null;
    }

    private static string? FindExecutable(string executable)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path)) return null;

        var extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Append(string.Empty)
                .ToArray()
            : new[] { string.Empty };

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory.Trim(), executable + extension);
                if (File.Exists(candidate)) return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Abre uma URL no navegador padrão do sistema.
    /// </summary>
    /// <param name="url">URL a abrir</param>
    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        var candidate = url.Trim();
        if (!candidate.Contains("://", StringComparison.Ordinal))
            candidate = "https://" + candidate;

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var parsed))
            return;

        var psi = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? parsed.ToString() : "xdg-open",
            UseShellExecute = OperatingSystem.IsWindows()
        };
        if (!OperatingSystem.IsWindows())
            psi.ArgumentList.Add(parsed.ToString());
        Process.Start(psi);
    }

    /// <summary>
    /// Abre conexão SSH em terminal.
    /// Usa porta 22 como padrão se não especificada.
    /// </summary>
    /// <param name="e">Acesso SSH com host, usuário e porta</param>
    private static void OpenSsh(AccessEntry e)
    {
        if (string.IsNullOrWhiteSpace(e.Host)) return;

        var port = (e.Porta is > 0) ? e.Porta.Value : 22;
        var userAt = string.IsNullOrWhiteSpace(e.Usuario) ? e.Host : $"{e.Usuario}@{e.Host}";

        if (OperatingSystem.IsWindows())
        {
            var ssh = FindExecutable("ssh.exe")
                ?? throw new FileNotFoundException("OpenSSH Client (ssh.exe) não encontrado. Instale o recurso opcional Cliente OpenSSH do Windows.");
            var psi = new ProcessStartInfo
            {
                FileName = ssh,
                UseShellExecute = true
            };
            psi.ArgumentList.Add("-p");
            psi.ArgumentList.Add(port.ToString(System.Globalization.CultureInfo.InvariantCulture));
            psi.ArgumentList.Add("--");
            psi.ArgumentList.Add(userAt);
            Process.Start(psi);
            return;
        }

        var cmd = $"ssh -p {port} {EscapeShell(userAt)}";

        OpenTerminal(cmd);
    }

    /// <summary>
    /// Abre acesso MTK/WinBox. Tenta clientes nativos e faz fallback para protocolo winbox://.
    /// </summary>
    private static void OpenMtk(AccessEntry e)
    {
        if (string.IsNullOrWhiteSpace(e.Host)) return;

        var host = e.Host.Trim();
        var port = (e.Porta is > 0) ? e.Porta.Value : 8291;
        var hostPort = $"{host}:{port}";
        var user = (e.Usuario ?? "").Trim();

        if (!string.IsNullOrWhiteSpace(user) && TryStartProcess("WinBox", hostPort, user)) return;
        if (TryStartProcess("WinBox", hostPort)) return;
        if (!string.IsNullOrWhiteSpace(user) && TryStartProcess("winbox", hostPort, user)) return;
        if (TryStartProcess("winbox", hostPort)) return;
        if (!string.IsNullOrWhiteSpace(user) && TryStartProcess("WinBox4Linux", hostPort, user)) return;
        if (TryStartProcess("WinBox4Linux", hostPort)) return;
        if (!string.IsNullOrWhiteSpace(user) && TryStartProcess("winbox4linux", hostPort, user)) return;
        if (TryStartProcess("winbox4linux", hostPort)) return;

        if (!string.IsNullOrWhiteSpace(user) &&
            (TryStartProcess("WinBox4Linux", "--host", host, "--port", port.ToString(), "--user", user) ||
             TryStartProcess("winbox4linux", "--host", host, "--port", port.ToString(), "--user", user)))
        {
            return;
        }

        var url = string.IsNullOrWhiteSpace(user)
            ? $"winbox://{host}:{port}"
            : $"winbox://{Uri.EscapeDataString(user)}@{host}:{port}";
        OpenUrl(url);
    }

    /// <summary>
    /// Abre um terminal com um comando a executar.
    /// Tenta vários emuladores de terminal comuns até conseguir abrir um.
    /// </summary>
    /// <param name="command">Comando a executar no terminal</param>
    private static void OpenTerminal(string command)
    {
        // Tenta emuladores em ordem de preferência
        if (TryTerminal("x-terminal-emulator", "-e", "bash", "-lc", command)) return;
        if (TryTerminal("gnome-terminal", "--", "bash", "-lc", command)) return;
        if (TryTerminal("konsole", "-e", "bash", "-lc", command)) return;
        // Fallback para xterm
        TryTerminal("xterm", "-e", "bash", "-lc", command);
    }

    /// <summary>
    /// Tenta abrir um terminal com argumentos.
    /// Retorna sucesso se conseguir iniciar o processo.
    /// </summary>
    /// <param name="file">Nome/caminho do emulador de terminal</param>
    /// <param name="args">Argumentos para passar ao terminal</param>
    /// <returns>true se conseguiu iniciar, false caso contrário</returns>
    private static bool TryTerminal(string file, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = file,
                UseShellExecute = false
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryStartProcess(string file, params string[] args)
    {
        try
        {
            var executable = FindExecutable(file);
            if (executable is null) return false;
            var psi = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Escapa uma string para uso seguro em bash -lc.
    /// Substitui aspas simples por sequência de escape.
    /// </summary>
    /// <param name="s">String a escapar</param>
    /// <returns>String escapada para bash</returns>
    private static string EscapeShell(string s)
    {
        // Escapa aspas simples: ' -> '\"'\"'
        return s.Replace("'", "'\"'\"'");
    }
}
