using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XCommander.ViewModels;

public enum EncodingMode
{
    Base64,
    Base64Url,
    Hex,
    UrlEncode,
    HtmlEncode,
    UuEncode
}

public partial class EncodingToolViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _inputText = string.Empty;
    
    [ObservableProperty]
    private string _outputText = string.Empty;
    
    [ObservableProperty]
    private EncodingMode _selectedMode = EncodingMode.Base64;
    
    [ObservableProperty]
    private bool _isDecodeMode;
    
    [ObservableProperty]
    private string _statusMessage = string.Empty;
    
    [ObservableProperty]
    private bool _wrapLines = true;
    
    [ObservableProperty]
    private int _wrapLength = 76;
    
    public string[] AvailableModes { get; } = ["Base64", "Base64 URL", "Hex", "URL Encode", "HTML Encode", "UUEncode"];
    
    public event EventHandler? RequestClose;
    
    partial void OnInputTextChanged(string value)
    {
        // Auto-transform when input changes
        if (!string.IsNullOrEmpty(value))
        {
            Transform();
        }
        else
        {
            OutputText = string.Empty;
            StatusMessage = string.Empty;
        }
    }
    
    partial void OnSelectedModeChanged(EncodingMode value)
    {
        Transform();
    }
    
    partial void OnIsDecodeModeChanged(bool value)
    {
        Transform();
    }
    
    [RelayCommand]
    public void Transform()
    {
        if (string.IsNullOrEmpty(InputText))
        {
            OutputText = string.Empty;
            StatusMessage = string.Empty;
            return;
        }
        
        try
        {
            OutputText = IsDecodeMode ? Decode(InputText) : Encode(InputText);
            var inputLen = InputText.Length;
            var outputLen = OutputText.Length;
            StatusMessage = $"Input: {inputLen} chars → Output: {outputLen} chars";
        }
        catch (Exception ex)
        {
            OutputText = string.Empty;
            StatusMessage = $"Error: {ex.Message}";
        }
    }
    
    private string Encode(string input)
    {
        return SelectedMode switch
        {
            EncodingMode.Base64 => EncodeBase64(input),
            EncodingMode.Base64Url => EncodeBase64Url(input),
            EncodingMode.Hex => EncodeHex(input),
            EncodingMode.UrlEncode => Uri.EscapeDataString(input),
            EncodingMode.HtmlEncode => System.Net.WebUtility.HtmlEncode(input),
            EncodingMode.UuEncode => EncodeUuEncode(input),
            _ => input
        };
    }
    
    private string Decode(string input)
    {
        return SelectedMode switch
        {
            EncodingMode.Base64 => DecodeBase64(input),
            EncodingMode.Base64Url => DecodeBase64Url(input),
            EncodingMode.Hex => DecodeHex(input),
            EncodingMode.UrlEncode => Uri.UnescapeDataString(input),
            EncodingMode.HtmlEncode => System.Net.WebUtility.HtmlDecode(input),
            EncodingMode.UuEncode => DecodeUuEncode(input),
            _ => input
        };
    }
    
    private string EncodeBase64(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var result = Convert.ToBase64String(bytes);
        
        if (WrapLines && WrapLength > 0)
        {
            result = WrapText(result, WrapLength);
        }
        
        return result;
    }
    
    private string DecodeBase64(string input)
    {
        // Remove whitespace for decoding
        var cleanInput = new string(input.Where(c => !char.IsWhiteSpace(c)).ToArray());
        var bytes = Convert.FromBase64String(cleanInput);
        return Encoding.UTF8.GetString(bytes);
    }
    
    private string EncodeBase64Url(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var result = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        
        if (WrapLines && WrapLength > 0)
        {
            result = WrapText(result, WrapLength);
        }
        
        return result;
    }
    
    private string DecodeBase64Url(string input)
    {
        // Remove whitespace
        var cleanInput = new string(input.Where(c => !char.IsWhiteSpace(c)).ToArray());
        
        // Restore standard Base64 characters
        var base64 = cleanInput.Replace('-', '+').Replace('_', '/');
        
        // Pad with = if necessary
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        
        var bytes = Convert.FromBase64String(base64);
        return Encoding.UTF8.GetString(bytes);
    }
    
    private string EncodeHex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var result = Convert.ToHexString(bytes).ToLowerInvariant();
        
        if (WrapLines && WrapLength > 0)
        {
            result = WrapText(result, WrapLength);
        }
        
        return result;
    }
    
    private string DecodeHex(string input)
    {
        // Remove whitespace
        var cleanInput = new string(input.Where(c => !char.IsWhiteSpace(c)).ToArray());
        var bytes = Convert.FromHexString(cleanInput);
        return Encoding.UTF8.GetString(bytes);
    }
    
    private static string WrapText(string text, int lineLength)
    {
        if (string.IsNullOrEmpty(text) || lineLength <= 0)
            return text;
            
        var sb = new StringBuilder();
        for (int i = 0; i < text.Length; i += lineLength)
        {
            if (i > 0)
                sb.AppendLine();
            sb.Append(text.AsSpan(i, Math.Min(lineLength, text.Length - i)));
        }
        return sb.ToString();
    }
    
    [RelayCommand]
    public void SwapInputOutput()
    {
        (InputText, OutputText) = (OutputText, InputText);
    }
    
    [RelayCommand]
    public void ClearAll()
    {
        InputText = string.Empty;
        OutputText = string.Empty;
        StatusMessage = string.Empty;
    }
    
    [RelayCommand]
    public async Task EncodeFileAsync()
    {
        // Request file selection via event
        FileSelectionRequested?.Invoke(this, new FileSelectionEventArgs(false, EncodeSelectedFile));
    }
    
    [RelayCommand]
    public async Task DecodeToFileAsync()
    {
        // Request file save via event
        FileSelectionRequested?.Invoke(this, new FileSelectionEventArgs(true, DecodeToSelectedFile));
    }
    
    public event EventHandler<FileSelectionEventArgs>? FileSelectionRequested;
    
    private void EncodeSelectedFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return;
            
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var fileName = Path.GetFileName(filePath);
            
            OutputText = SelectedMode switch
            {
                EncodingMode.Base64 or EncodingMode.Base64Url => 
                    WrapLines && WrapLength > 0 
                        ? WrapText(Convert.ToBase64String(bytes), WrapLength) 
                        : Convert.ToBase64String(bytes),
                EncodingMode.Hex => 
                    WrapLines && WrapLength > 0 
                        ? WrapText(Convert.ToHexString(bytes).ToLowerInvariant(), WrapLength) 
                        : Convert.ToHexString(bytes).ToLowerInvariant(),
                _ => Convert.ToBase64String(bytes)
            };
            
            StatusMessage = $"Encoded file: {fileName} ({bytes.Length} bytes) → {OutputText.Length} chars";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error encoding file: {ex.Message}";
        }
    }
    
    private void DecodeToSelectedFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(OutputText))
            return;
            
        try
        {
            var cleanInput = new string(OutputText.Where(c => !char.IsWhiteSpace(c)).ToArray());
            var bytes = SelectedMode switch
            {
                EncodingMode.Base64 => Convert.FromBase64String(cleanInput),
                EncodingMode.Base64Url => Convert.FromBase64String(
                    cleanInput.Replace('-', '+').Replace('_', '/') + 
                    new string('=', (4 - cleanInput.Length % 4) % 4)),
                EncodingMode.Hex => Convert.FromHexString(cleanInput),
                _ => Convert.FromBase64String(cleanInput)
            };
            
            File.WriteAllBytes(filePath, bytes);
            StatusMessage = $"Decoded to file: {Path.GetFileName(filePath)} ({bytes.Length} bytes)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error decoding to file: {ex.Message}";
        }
    }
    
    private string EncodeUuEncode(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var sb = new StringBuilder();
        
        // UUEncode header
        sb.AppendLine("begin 644 data");
        
        // Process in 45-byte chunks (becomes 60 chars per line)
        for (int i = 0; i < bytes.Length; i += 45)
        {
            int chunkLength = Math.Min(45, bytes.Length - i);
            var chunk = new byte[chunkLength];
            Array.Copy(bytes, i, chunk, 0, chunkLength);
            
            // Length character
            sb.Append((char)(chunkLength + 32));
            
            // Encode 3 bytes into 4 characters
            for (int j = 0; j < chunkLength; j += 3)
            {
                int b0 = chunk[j];
                int b1 = j + 1 < chunkLength ? chunk[j + 1] : 0;
                int b2 = j + 2 < chunkLength ? chunk[j + 2] : 0;
                
                sb.Append((char)(((b0 >> 2) & 0x3F) + 32));
                sb.Append((char)((((b0 << 4) | (b1 >> 4)) & 0x3F) + 32));
                sb.Append((char)((((b1 << 2) | (b2 >> 6)) & 0x3F) + 32));
                sb.Append((char)((b2 & 0x3F) + 32));
            }
            
            sb.AppendLine();
        }
        
        // End marker
        sb.AppendLine("`");
        sb.AppendLine("end");
        
        return sb.ToString();
    }
    
    private string DecodeUuEncode(string input)
    {
        var lines = input.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\r'))
            .ToList();
        
        // Skip header line if present
        int startLine = 0;
        if (lines.Count > 0 && lines[0].StartsWith("begin ", StringComparison.OrdinalIgnoreCase))
        {
            startLine = 1;
        }
        
        var bytes = new List<byte>();
        
        for (int i = startLine; i < lines.Count; i++)
        {
            var line = lines[i];
            
            // End markers
            if (line == "`" || line == " " || line.StartsWith("end", StringComparison.OrdinalIgnoreCase))
                break;
            
            if (line.Length == 0)
                continue;
            
            // First char is length
            int length = (line[0] - 32) & 0x3F;
            if (length == 0)
                break;
            
            // Decode 4 chars into 3 bytes
            for (int j = 1, byteCount = 0; j < line.Length - 3 && byteCount < length; j += 4)
            {
                int c0 = (line[j] - 32) & 0x3F;
                int c1 = (line[j + 1] - 32) & 0x3F;
                int c2 = (line[j + 2] - 32) & 0x3F;
                int c3 = (line[j + 3] - 32) & 0x3F;
                
                if (byteCount < length)
                {
                    bytes.Add((byte)((c0 << 2) | (c1 >> 4)));
                    byteCount++;
                }
                if (byteCount < length)
                {
                    bytes.Add((byte)((c1 << 4) | (c2 >> 2)));
                    byteCount++;
                }
                if (byteCount < length)
                {
                    bytes.Add((byte)((c2 << 6) | c3));
                    byteCount++;
                }
            }
        }
        
        return Encoding.UTF8.GetString(bytes.ToArray());
    }
    
    [RelayCommand]
    public void Close()
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}

public class FileSelectionEventArgs : EventArgs
{
    public bool IsSaveDialog { get; }
    public Action<string?> Callback { get; }
    
    public FileSelectionEventArgs(bool isSaveDialog, Action<string?> callback)
    {
        IsSaveDialog = isSaveDialog;
        Callback = callback;
    }
}
