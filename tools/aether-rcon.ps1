param([string]$Pass="aether", [string]$Cmd="css_plugins list", [string]$Server="127.0.0.1", [int]$Port=27015)
# Минимальный клиент Source RCON (TCP). Возвращает ответ сервера.
function Send-Packet($stream,[int]$id,[int]$type,[string]$body){
  $bodyBytes=[System.Text.Encoding]::ASCII.GetBytes($body)
  $size=4+4+$bodyBytes.Length+2
  $w=New-Object System.IO.BinaryWriter($stream)
  $w.Write([int]$size); $w.Write([int]$id); $w.Write([int]$type)
  $w.Write($bodyBytes); $w.Write([byte]0); $w.Write([byte]0); $w.Flush()
}
function Read-Packet($stream){
  $r=New-Object System.IO.BinaryReader($stream)
  $size=$r.ReadInt32(); $id=$r.ReadInt32(); $type=$r.ReadInt32()
  $bytes=$r.ReadBytes($size-8); $body=[System.Text.Encoding]::ASCII.GetString($bytes).TrimEnd([char]0)
  return @{ Id=$id; Type=$type; Body=$body }
}
try {
  $client=New-Object System.Net.Sockets.TcpClient
  $client.Connect($Server,$Port)
  $ns=$client.GetStream()
  Send-Packet $ns 1 3 $Pass            # SERVERDATA_AUTH
  $auth=Read-Packet $ns                # may be value-response
  if($auth.Id -eq -1){ "AUTH FAILED"; exit 1 }
  Send-Packet $ns 2 2 $Cmd             # SERVERDATA_EXECCOMMAND
  Start-Sleep -Milliseconds 300
  $resp=Read-Packet $ns
  $resp.Body
  $client.Close()
} catch { "RCON ERROR: $($_.Exception.Message)" }