"""Read-only audit of Git candidates. Prints locations/categories, never matched values.
No detector can prove the absence of every secret; review binary/reference assets separately.
"""
from pathlib import Path
import json, re, subprocess, sys

ROOT=Path(__file__).resolve().parents[1]
def git(*args):
    return subprocess.check_output(['git','-c',f'safe.directory={ROOT.as_posix()}',*args],cwd=ROOT)
paths=sorted(set(p.decode('utf-8') for p in git('ls-files','--cached','--others','--exclude-standard','-z').split(b'\0') if p))
findings=[];binaries=[];checked=0
private_names=re.compile(r'(?i)(?:^|/)(?:\.local|node_modules|bin|obj|\.aws|\.azure|\.codex|\.agents|TestResults)(?:/|$)|(?:^|/)(?:appsettings(?:\.[\w-]+)*\.(?:Local|Secrets)\.json|\.env(?:\..*)?)$|\.(?:mdf|ldf|ndf|bak|bacpac|pfx|p12|key|pem|tmp|temp|trx|mail|local\.json)$')
patterns={
 'private_key':r'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----',
 'github_token':r'\b(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,})\b',
 'aws_access_key':r'\b(?:AKIA|ASIA)[A-Z0-9]{16}\b',
 'sql_password_literal':r'(?i)\b(?:password|pwd)\s*=\s*[^\s;"\'<>][^;"\'\r\n<>]{2,}',
 'email_address':r'\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b',
}
allowed_domains=('example.test','example.com','example.org','example.net','localhost','correo.com','ejemplo.com')
for name in paths:
    file=ROOT/name
    if private_names.search(name):findings.append({'file':name,'category':'excluded_file_already_tracked'});continue
    if not file.is_file():continue
    raw=file.read_bytes()
    if b'\0' in raw or file.suffix.lower() in ('.pdf','.docx','.png','.jpg','.jpeg','.gif','.woff','.woff2','.ttf','.ico'):
        binaries.append(name);continue
    text=raw.decode('utf-8-sig',errors='replace');checked+=1
    for category,pattern in patterns.items():
        # Vendor licenses/credits use maintainers' public addresses; not application data.
        if category=='email_address' and ('/wwwroot/lib/' in '/'+name or name.endswith('package-lock.json')):continue
        for match in re.finditer(pattern,text):
            value=match.group()
            line=text.splitlines()[text.count('\n',0,match.start())]
            if category=='email_address' and value.split('@')[-1].lower() in allowed_domains:continue
            if category=='email_address' and name.endswith('.cshtml') and re.search(r'@(?:Model|publicacion|comentario|item|u|message)\.',value):continue
            if category=='sql_password_literal' and not re.search(r'(?i)(?:server|data source|database)\s*=',line):continue
            if category=='sql_password_literal' and name.endswith('DatabaseConnectionPolicyTests.cs') and 'shared.invalid' in line and 'private-sentinel' in value:continue
            if category=='sql_password_literal' and re.search(r'(?i)synthetic|placeholder|example|<|\$|\{',value):continue
            findings.append({'file':name,'line':text.count('\n',0,match.start())+1,'category':category})
    if name.endswith('.json') and 'appsettings' in file.name:
        try:data=json.loads(text)
        except json.JSONDecodeError:findings.append({'file':name,'category':'invalid_configuration_json'});continue
        def walk(node,keys=()):
            if isinstance(node,dict):
                for k,v in node.items():walk(v,keys+(k,))
            elif isinstance(node,str) and keys and 'connection' in '.'.join(keys).lower() and node.strip():
                server=re.search(r'(?i)(?:server|data source)\s*=\s*([^;]+)',node)
                if server and not re.fullmatch(r'(?i)(?:\(localdb\)\\[\w-]+|localhost|127\.0\.0\.1|\.)',server.group(1).strip()):
                    findings.append({'file':name,'category':'nonlocal_connection_string','key':'.'.join(keys)})
        walk(data)
report={'candidateFiles':len(paths),'textFilesReviewed':checked,'findings':findings,'binaryAssetsForManualReview':binaries}
destination=ROOT/'.local/final-review';destination.mkdir(parents=True,exist_ok=True)
(destination/'repository-safety.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'candidateFiles':len(paths),'textFilesReviewed':checked,'findings':findings,'binaryAssetCount':len(binaries)},ensure_ascii=False,indent=2))
sys.exit(1 if findings else 0)
