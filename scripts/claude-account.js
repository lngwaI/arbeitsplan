// Account-Kennung: liest die Login-Mail von Claude Code und gibt nur das Label aus (nie die Mail).
var fs = require('fs'), os = require('os'), path = require('path');

function email() {
  try {
    var j = JSON.parse(fs.readFileSync(path.join(os.homedir(), '.claude.json'), 'utf8'));
    return ((j.oauthAccount && j.oauthAccount.emailAddress) || '').toLowerCase();
  } catch (e) { return ''; }
}

var m = email(), a;
if (/@gmail\.com$/.test(m)) a = { label: 'G-Mail / 2. Account', slug: 'gmail' };
else if (/@gmx\.de$/.test(m)) a = { label: 'GMX / Haupt-Account', slug: 'gmx' };
else a = { label: 'Unbekannt', slug: 'unbekannt' };

var arg = process.argv[2];
console.log(arg === '--slug' ? a.slug : arg === '--trailer' ? 'Claude-Account: ' + a.label : a.label);
