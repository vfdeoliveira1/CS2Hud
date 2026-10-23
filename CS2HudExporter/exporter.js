// CS2 HUD Exporter (Node.js)
//
// Recebe os dados do CS2 (Game State Integration) na porta 3000 e entrega
// a API da HUD na porta 5000 (http://localhost:5000/api/hud).
//
// Por que Node.js: o Controle Inteligente de Aplicativos do Windows
// bloqueia DLLs/EXEs compilados na própria máquina (sem assinatura
// digital), então a versão em .NET não abria. Aqui não existe nada
// compilado: o node.exe é assinado e este arquivo é só texto.
// Não usa nenhum pacote (npm install não é necessário).

'use strict';

const http = require('http');
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const { execFileSync } = require('child_process');

const GSI_PORT = 3000;
const API_PORT = 5000;
const DATA_DIR = __dirname; // teams.json, series.json e cams.json ficam aqui

// =========================================================================
// Helpers
// =========================================================================

// leitura tolerante: o GSI manda números às vezes como número, às vezes
// como texto ("12.5"), e booleanos como true/1
function str(v){
  if (v === undefined || v === null || typeof v === 'object') return '';
  return String(v);
}
function num(v){
  if (typeof v === 'number') return isFinite(v) ? v : 0;
  if (typeof v === 'boolean') return v ? 1 : 0;
  const d = parseFloat(str(v));
  return isFinite(d) ? d : 0;
}
function int(v){ return Math.round(num(v)); }
function bool(v){
  if (typeof v === 'boolean') return v;
  return num(v) !== 0 || str(v).toLowerCase() === 'true';
}
function obj(v){ return v && typeof v === 'object' && !Array.isArray(v) ? v : null; }

// "x, y, z" -> { x, y, z } (ponto como separador decimal)
function parseVector(text){
  text = str(text).trim();
  if (!text) return null;
  const parts = text.split(',');
  if (parts.length !== 3) return null;
  const n = parts.map(p => parseFloat(p.trim()));
  if (n.some(x => !isFinite(x))) return null;
  return { x: n[0], y: n[1], z: n[2] };
}

// campo de um objeto JSON aceitando "nome" ou "Nome" (os .json antigos,
// salvos pela versão .NET, usam a primeira letra maiúscula)
function get(o, key){
  if (!o) return undefined;
  if (o[key] !== undefined) return o[key];
  const cap = key[0].toUpperCase() + key.slice(1);
  return o[cap];
}

// o CS2 manda nomes com caracteres especiais em HTML ("&quot;30FPS&quot;")
function htmlDecode(t){
  t = str(t);
  if (t.indexOf('&') === -1) return t;
  return t.replace(/&#x([0-9a-f]+);/gi, (m, h) => String.fromCodePoint(parseInt(h, 16)))
    .replace(/&#(\d+);/g, (m, d) => String.fromCodePoint(parseInt(d, 10)))
    .replace(/&quot;/g, '"').replace(/&apos;/g, "'").replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&amp;/g, '&');
}

function trimMax(s, max){
  s = str(s).trim();
  return s.length > max ? s.slice(0, max) : s;
}

function readJson(file){
  try {
    if (fs.existsSync(file)) return JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, ''));
  } catch (e) {
    console.log('[AVISO] Não consegui ler ' + file + ': ' + e.message);
  }
  return null;
}
function writeJson(file, data){
  try {
    if (data === null) { if (fs.existsSync(file)) fs.unlinkSync(file); }
    else fs.writeFileSync(file, JSON.stringify(data, null, 2));
  } catch (e) {
    console.log('[AVISO] Não consegui salvar ' + file + ': ' + e.message);
  }
}

// =========================================================================
// Times (control.html) — guarda os steamIds de cada time para o nome/logo
// acompanharem o TIME quando ele troca de lado
// =========================================================================
const TEAMS_FILE = path.join(DATA_DIR, 'teams.json');
const teamsStore = {
  teams: [], bestOf: 0, forcePause: false,
  // modo X1 (1 contra 1) e, nele, radar e série ligados/desligados
  x1: false, x1Radar: true, x1Series: true,

  load(){
    const data = readJson(TEAMS_FILE);
    if (!data) return;
    const list = Array.isArray(data) ? data : (get(data, 'teams') || []);
    if (!Array.isArray(data)){
      this.bestOf = int(get(data, 'bestOf'));
      this.forcePause = bool(get(data, 'forcePause'));
      this.x1 = bool(get(data, 'x1'));
      this.x1Radar = get(data, 'x1Radar') === undefined ? true : bool(get(data, 'x1Radar'));
      this.x1Series = get(data, 'x1Series') === undefined ? true : bool(get(data, 'x1Series'));
    }
    this.teams = list.map(t => ({
      id: str(get(t, 'id')) || newId(),
      name: str(get(t, 'name')),
      logo: str(get(t, 'logo')),
      side: str(get(t, 'side')) || 'CT',
      mapWins: int(get(t, 'mapWins')),
      coach: str(get(t, 'coach')),
      steamIds: (get(t, 'steamIds') || []).map(str),
    }));
  },

  save(){
    writeJson(TEAMS_FILE, {
      BestOf: this.bestOf, ForcePause: this.forcePause,
      X1: this.x1, X1Radar: this.x1Radar, X1Series: this.x1Series,
      Teams: this.teams.map(t => ({ Id: t.id, Name: t.name, Logo: t.logo, Side: t.side, MapWins: t.mapWins, Coach: t.coach, SteamIds: t.steamIds })),
    });
  },

  // times pelo lado atual: se a maioria dos jogadores de um time agora está
  // do outro lado, o time trocou de lado
  resolve(players, includeLogoData){
    const ct = idsOnSide(players, 'CT'), t = idsOnSide(players, 'T');
    let changed = false;

    for (const team of this.teams){
      if (team.steamIds.length === 0){
        // salvo antes da partida ter jogadores: grava o elenco assim que eles aparecerem
        const ids = team.side === 'CT' ? ct : t;
        if (ids.size > 0){ team.steamIds = [...ids]; changed = true; }
        continue;
      }
      const inCt = team.steamIds.filter(i => ct.has(i)).length;
      const inT = team.steamIds.filter(i => t.has(i)).length;
      if (inCt === inT) continue;
      const side = inCt > inT ? 'CT' : 'T';
      if (side !== team.side){ team.side = side; changed = true; }
    }

    // dois times no mesmo lado não faz sentido: ajusta o segundo
    if (this.teams.length === 2 && this.teams[0].side === this.teams[1].side){
      this.teams[1].side = this.teams[0].side === 'CT' ? 'T' : 'CT';
      changed = true;
    }

    // jogador novo (substituto) passa a contar pro time do lado dele
    if (this.teams.length > 0){
      const known = new Set(this.teams.flatMap(x => x.steamIds));
      for (const team of this.teams){
        for (const id of (team.side === 'CT' ? ct : t)){
          if (!known.has(id)){ known.add(id); team.steamIds.push(id); changed = true; }
        }
      }
    }

    if (changed) this.save();
    return this.toDto(includeLogoData);
  },

  set(input, players){
    this.teams = [];
    const bo = int(get(input, 'bestOf'));
    this.bestOf = [1, 3, 5, 7].includes(bo) ? bo : 0;
    this.forcePause = bool(get(input, 'forcePause'));
    this.x1 = bool(get(input, 'x1'));
    this.x1Radar = get(input, 'x1Radar') === undefined ? true : bool(get(input, 'x1Radar'));
    this.x1Series = get(input, 'x1Series') === undefined ? true : bool(get(input, 'x1Series'));
    this.addTeam(obj(get(input, 'ct')), 'CT', players);
    this.addTeam(obj(get(input, 't')), 'T', players);
    this.save();
  },

  swap(players){
    for (const team of this.teams){
      team.side = team.side === 'CT' ? 'T' : 'CT';
      team.steamIds = [...idsOnSide(players, team.side)];
    }
    this.save();
  },

  clear(){
    this.teams = []; this.bestOf = 0; this.forcePause = false;
    this.x1 = false; this.x1Radar = true; this.x1Series = true;
    this.save();
  },

  addTeam(info, side, players){
    if (!info) return;
    const name = trimMax(get(info, 'name'), 40);
    let logo = str(get(info, 'logo'));
    if (!logo.toLowerCase().startsWith('data:image/')) logo = '';
    const maxWins = Math.floor((this.bestOf + 1) / 2);
    const mapWins = Math.max(0, Math.min(int(get(info, 'mapWins')), maxWins));
    const coach = trimMax(get(info, 'coach'), 40);
    if (!name && !logo && !mapWins && !coach) return; // lado sem nada = padrão
    this.teams.push({ id: newId(), name, logo, mapWins, coach, side, steamIds: [...idsOnSide(players, side)] });
  },

  // imagem da logo a partir do data URL salvo (data:image/png;base64,AAAA...)
  getLogo(id){
    const team = this.teams.find(x => x.id === id);
    if (!team || !team.logo) return null;
    const comma = team.logo.indexOf(','), semi = team.logo.indexOf(';');
    if (comma < 0 || semi < 0 || semi > comma) return null;
    return { bytes: Buffer.from(team.logo.slice(comma + 1), 'base64'), type: team.logo.slice(5, semi) };
  },

  toDto(includeLogoData){
    const dto = { ct: null, t: null, bestOf: this.bestOf, forcePause: this.forcePause,
      x1: this.x1, x1Radar: this.x1Radar, x1Series: this.x1Series };
    for (const team of this.teams){
      const info = {
        name: team.name,
        logo: includeLogoData ? team.logo : '',
        // id muda a cada "Salvar", então a HUD recarrega a imagem nova
        logoUrl: team.logo ? '/api/teams/logo/' + team.id : '',
        mapWins: team.mapWins,
        coach: team.coach,
      };
      if (team.side === 'CT') dto.ct = info; else dto.t = info;
    }
    return dto;
  },
};

function newId(){ return crypto.randomBytes(16).toString('hex'); }

function idsOnSide(players, side){
  return new Set(players.filter(p => p.team === side && p.steamId).map(p => p.steamId));
}

// =========================================================================
// Série de mapas (CS2MapVeto)
// =========================================================================
const SERIES_FILE = path.join(DATA_DIR, 'series.json');
const seriesStore = {
  series: null,

  load(){
    const data = readJson(SERIES_FILE);
    this.series = data ? this.clean(data) : null;
  },

  clean(input){
    if (!obj(input)) return null;
    let maps = (get(input, 'maps') || []).slice(0, 7).map(m => {
      const picked = str(get(m, 'pickedBy')), winner = str(get(m, 'winner'));
      const sa = get(m, 'scoreA'), sb = get(m, 'scoreB');
      return {
        id: trimMax(get(m, 'id'), 40),
        name: trimMax(get(m, 'name'), 40),
        pickedBy: picked === 'A' || picked === 'B' ? picked : '',
        scoreA: sa === null || sa === undefined || sa === '' ? null : int(sa),
        scoreB: sb === null || sb === undefined || sb === '' ? null : int(sb),
        winner: winner === 'A' || winner === 'B' ? winner : '',
      };
    });
    if (maps.length === 0) return null;
    let current = get(input, 'current');
    current = current === undefined ? -1 : int(current);
    if (current < -1 || current >= maps.length) current = -1;
    return {
      bestOf: int(get(input, 'bestOf')),
      teamA: trimMax(get(input, 'teamA'), 40),
      teamB: trimMax(get(input, 'teamB'), 40),
      maps, current,
    };
  },

  set(input){
    this.series = input ? this.clean(input) : null;
    const s = this.series;
    writeJson(SERIES_FILE, s && {
      BestOf: s.bestOf, TeamA: s.teamA, TeamB: s.teamB, Current: s.current,
      Maps: s.maps.map(m => ({ Id: m.id, Name: m.name, PickedBy: m.pickedBy, ScoreA: m.scoreA, ScoreB: m.scoreB, Winner: m.winner })),
    });
  },
};

// =========================================================================
// Webcams (VDO.Ninja, control.html)
// =========================================================================
const CAMS_FILE = path.join(DATA_DIR, 'cams.json');
// IDs/senha do VDO.Ninja: só letras, números, _ e - (vão na URL)
function cleanCamId(s){ return str(s).trim().replace(/[^\p{L}\p{N}_-]/gu, '').slice(0, 60); }
const camsStore = {
  cams: { enabled: false, password: '', ids: {} },

  load(){
    const data = readJson(CAMS_FILE);
    if (data) this.set(data, false);
  },

  set(input, save = true){
    const ids = {};
    const src = obj(get(input, 'ids')) || {};
    for (const k of Object.keys(src)){
      const id = cleanCamId(src[k]);
      if (k && id) ids[k] = id;
    }
    this.cams = { enabled: bool(get(input, 'enabled')), password: cleanCamId(get(input, 'password')), ids };
    if (save) writeJson(CAMS_FILE, { Enabled: this.cams.enabled, Password: this.cams.password, Ids: ids });
  },
};

// =========================================================================
// Configuração do GSI na pasta do CS2 (acha o jogo pela Steam)
// =========================================================================
const GSI_FILE = 'gamestate_integration_hudexporter.cfg';
const GSI_CONTENT = [
  '"HudExporter Integration Configuration"',
  '{',
  '    "uri"          "http://localhost:3000/"',
  '    "timeout"      "5.0"',
  '    "buffer"       "0.1"',
  '    "throttle"     "0.1"',
  '    "heartbeat"    "10.0"',
  '    "data"',
  '    {',
  '        "provider"                  "1"',
  '        "map"                       "1"',
  '        "map_round_wins"            "1"',
  '        "round"                     "1"',
  '        "player_id"                 "1"',
  '        "player_state"              "1"',
  '        "player_weapons"            "1"',
  '        "player_match_stats"        "1"',
  '        "player_position"           "1"',
  '        "allplayers_id"             "1"',
  '        "allplayers_state"          "1"',
  '        "allplayers_match_stats"    "1"',
  '        "allplayers_weapons"        "1"',
  '        "allplayers_position"       "1"',
  '        "phase_countdowns"          "1"',
  '        "allgrenades"               "1"',
  '        "bomb"                      "1"',
  '    }',
  '}',
  '',
].join('\n');

function steamRoots(){
  const roots = [];
  if (process.platform === 'win32'){
    try {
      const out = execFileSync('reg', ['query', 'HKCU\\Software\\Valve\\Steam', '/v', 'SteamPath'], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] });
      const m = out.match(/SteamPath\s+REG_SZ\s+(.+)/);
      if (m) roots.push(m[1].trim().replace(/\//g, '\\'));
    } catch (e) { /* sem acesso ao registro: segue com os caminhos padrão */ }
    roots.push('C:\\Program Files (x86)\\Steam', 'C:\\Program Files\\Steam');
  } else {
    const home = require('os').homedir();
    roots.push(path.join(home, '.steam', 'steam'), path.join(home, '.local', 'share', 'Steam'));
  }
  const seen = new Set();
  return roots.filter(r => {
    const k = r.toLowerCase();
    if (seen.has(k) || !fs.existsSync(r)) return false;
    seen.add(k); return true;
  });
}

// todas as bibliotecas da Steam (o CS2 pode estar em outro disco)
function libraryFolders(steamRoot){
  const result = [steamRoot];
  try {
    const text = fs.readFileSync(path.join(steamRoot, 'steamapps', 'libraryfolders.vdf'), 'utf8');
    const re = /"path"\s+"([^"]+)"/g;
    let m;
    while ((m = re.exec(text))) result.push(m[1].replace(/\\\\/g, '\\'));
  } catch (e) { /* sem o arquivo: só a pasta principal */ }
  return result;
}

function writeGsiConfig(){
  for (const steam of steamRoots()){
    for (const lib of libraryFolders(steam)){
      const cfgDir = path.join(lib, 'steamapps', 'common', 'Counter-Strike Global Offensive', 'game', 'csgo', 'cfg');
      if (!fs.existsSync(cfgDir)) continue;
      const file = path.join(cfgDir, GSI_FILE);
      const current = fs.existsSync(file) ? fs.readFileSync(file, 'utf8') : null;
      if (current !== GSI_CONTENT){
        fs.writeFileSync(file, GSI_CONTENT);
        console.log('[GSI] Configuração gravada em ' + file);
        console.log('      Se o CS2 já estava aberto, reinicie o jogo pra ele carregar.');
      }
      return file;
    }
  }
  return null;
}

// =========================================================================
// JSON do GSI -> estado da HUD
// Formato do GSI (resumido):
//   map:      { name, phase, round, team_ct: { score, name,
//               consecutive_round_losses, timeouts_remaining }, team_t: {...} }
//   round:    { phase: freezetime/live/over, bomb: planted/exploded/defused }
//   phase_countdowns: { phase: live/bomb/defuse/paused/timeout_ct/..., phase_ends_in }
//   bomb:     { state, position: "x, y, z", countdown }
//   player:   jogador da câmera (ou você, jogando)
//   allplayers: { "<steamid>": { name, team, state, match_stats, weapons, position, forward, observer_slot } }
//   grenades: { "<id>": { owner, type, position, velocity, lifetime, effecttime, flames } }
// =========================================================================
function mapGameState(gs){
  const map = obj(gs.map), round = obj(gs.round);

  const hud = {
    updatedAt: new Date().toISOString(),
    map: str(map && map.name),
    phase: str(map && map.phase),
    round: int(map && map.round),
    bombState: str(round && round.bomb),
    scoreboard: { ct: mapTeam(obj(map && map.team_ct)), t: mapTeam(obj(map && map.team_t)) },
    players: [],
    phaseSecondsLeft: null,
    phaseCountdownName: '',
    bomb: { state: '', position: null, countdown: 0 },
    spectatedPlayer: null,
    roundPhase: str(round && round.phase),
    grenades: mapGrenades(gs),
    roundWins: [],
    teams: null, series: null, cams: null,
  };

  // como cada round terminou: "round_wins": { "1": "ct_win_elimination", ... }
  const wins = obj(map && map.round_wins);
  if (wins){
    for (const k of Object.keys(wins)){
      const n = parseInt(k, 10);
      if (isFinite(n)) hud.roundWins.push({ round: n, result: str(wins[k]) });
    }
    hud.roundWins.sort((a, b) => a.round - b.round);
  }

  // tempo restante da fase (só vem espectando)
  const pc = obj(gs.phase_countdowns);
  if (pc){
    hud.phaseCountdownName = str(pc.phase);
    hud.phaseSecondsLeft = pc.phase_ends_in !== undefined && pc.phase_ends_in !== null ? num(pc.phase_ends_in) : null;
  }

  const bomb = obj(gs.bomb);
  if (bomb) hud.bomb = { state: str(bomb.state), position: parseVector(bomb.position), countdown: num(bomb.countdown) };

  // "player" é a câmera ativa: você jogando, ou quem está sendo espectado
  const player = obj(gs.player);
  hud.spectatedPlayer = player ? mapPlayer(str(player.steamid), player) : null;

  // "allplayers" só vem espectando; jogando, só o seu próprio jogador
  const all = obj(gs.allplayers);
  if (all && Object.keys(all).length > 0){
    for (const k of Object.keys(all)){
      const p = obj(all[k]);
      if (p) hud.players.push(mapPlayer(k, p));
    }
  } else if (hud.spectatedPlayer){
    hud.players.push(hud.spectatedPlayer);
  }

  return hud;
}

function mapTeam(team){
  return {
    teamName: htmlDecode(team && team.name),
    score: int(team && team.score),
    consecutiveRoundLosses: int(team && team.consecutive_round_losses),
    timeoutsRemaining: int(team && team.timeouts_remaining),
  };
}

const PRIMARY_TYPES = ['rifle', 'sniperrifle', 'submachinegun', 'shotgun', 'machinegun'];

function mapPlayer(steamId, player){
  const state = obj(player.state) || {}, stats = obj(player.match_stats) || {};
  const armor = int(state.armor);
  const slot = player.observer_slot;

  const dto = {
    steamId,
    nick: htmlDecode(player.name),
    team: str(player.team),
    health: int(state.health),
    armor,
    hasArmor: armor > 0,
    hasHelmet: bool(state.helmet),
    hasDefuseKit: bool(state.defusekit),
    activeWeapon: '',
    ammoClip: 0, ammoClipMax: 0, ammoReserve: 0,
    kills: int(stats.kills),
    deaths: int(stats.deaths),
    assists: int(stats.assists),
    roundKills: int(state.round_kills),
    roundKillHs: int(state.round_killhs),
    flashed: int(state.flashed),
    money: int(state.money),
    equipmentValue: int(state.equip_value),
    utility: [],
    primary: '',
    secondary: '',
    // sem posição fica 0,0,0 - a HUD trata isso como "sem posição"
    position: parseVector(player.position) || { x: 0, y: 0, z: 0 },
    forward: parseVector(player.forward),
    observerSlot: slot !== undefined && slot !== null && typeof slot !== 'object' ? int(slot) : -1,
  };

  const weapons = obj(player.weapons);
  if (weapons){
    for (const k of Object.keys(weapons)){
      const w = obj(weapons[k]);
      if (!w) continue;
      const name = str(w.name);
      // "Submachine Gun" -> "submachinegun"
      const type = str(w.type).replace(/ /g, '').toLowerCase();
      if (str(w.state) === 'active'){
        dto.activeWeapon = name;
        dto.ammoClip = int(w.ammo_clip);
        dto.ammoClipMax = int(w.ammo_clip_max);
        dto.ammoReserve = int(w.ammo_reserve);
      }
      if (type === 'grenade' || type === 'c4') dto.utility.push(name);
      else if (type === 'pistol') dto.secondary = name;
      else if (PRIMARY_TYPES.includes(type)) dto.primary = name;
    }
  }
  return dto;
}

function mapGrenades(gs){
  const list = [];
  const grenades = obj(gs.grenades);
  if (!grenades) return list;
  for (const k of Object.keys(grenades)){
    const g = obj(grenades[k]);
    if (!g) continue;
    const flames = [];
    const f = obj(g.flames);
    if (f) for (const fk of Object.keys(f)){ const pos = parseVector(f[fk]); if (pos) flames.push(pos); }
    list.push({
      id: k,
      owner: str(g.owner),
      type: str(g.type),
      position: parseVector(g.position),
      velocity: parseVector(g.velocity),
      lifetime: num(g.lifetime),
      effectTime: num(g.effecttime),
      flames,
    });
  }
  return list;
}

// =========================================================================
// Servidor HTTP
// =========================================================================
let latest = null;
function currentPlayers(){ return latest ? latest.players : []; }

const CORS = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Methods': 'GET, POST, DELETE, OPTIONS',
  'Access-Control-Allow-Headers': '*',
};

function send(res, code, body, type){
  const headers = Object.assign({}, CORS);
  if (body === undefined || code === 204){ res.writeHead(code, headers); res.end(); return; }
  if (Buffer.isBuffer(body)){ headers['Content-Type'] = type; res.writeHead(code, headers); res.end(body); return; }
  if (typeof body === 'string'){ headers['Content-Type'] = 'text/plain; charset=utf-8'; res.writeHead(code, headers); res.end(body); return; }
  headers['Content-Type'] = 'application/json; charset=utf-8';
  res.writeHead(code, headers);
  res.end(JSON.stringify(body));
}

function parseBody(body){
  try { return body ? JSON.parse(body) : null; } catch (e) { return null; }
}

// =========================================================================
// Páginas (HUD, painel, veto) servidas por http://localhost:5000
// No OBS, a HUD aberta como "arquivo local" não consegue tocar o vídeo das
// câmeras do VDO.Ninja (iframe); pela URL http funciona como no Chrome.
// =========================================================================
const STATIC_DIRS = {
  '/hud/': path.join(__dirname, '..', 'CS2Hud'),
  '/veto/': path.join(__dirname, '..', 'CS2MapVeto'),
};
const MIME = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8', '.svg': 'image/svg+xml', '.png': 'image/png',
  '.jpg': 'image/jpeg', '.jpeg': 'image/jpeg', '.webp': 'image/webp', '.gif': 'image/gif', '.ico': 'image/x-icon',
};
// página do veto: aceita "index.html" ou "veto_index.html" (o nome que estiver na pasta)
const VETO_NAMES = ['index.html', 'veto_index.html'];
function vetoFileName(){
  const dir = STATIC_DIRS['/veto/'];
  return VETO_NAMES.find(n => fs.existsSync(path.join(dir, n))) || VETO_NAMES[0];
}

function serveStatic(url, res){
  for (const prefix of Object.keys(STATIC_DIRS)){
    if (!url.startsWith(prefix)) continue;
    const root = STATIC_DIRS[prefix];
    let rel;
    try { rel = decodeURIComponent(url.slice(prefix.length)) || 'index.html'; } catch (e) { return send(res, 400, 'Bad request'), true; }
    // pediu um dos nomes da página do veto: entrega o que existir
    if (prefix === '/veto/' && VETO_NAMES.includes(rel)) rel = vetoFileName();
    const file = path.resolve(root, rel);
    // não deixa sair da pasta (ex: /hud/../../arquivo)
    if (file !== root && !file.startsWith(root + path.sep)) return send(res, 403, 'Forbidden'), true;
    fs.readFile(file, (err, data) => {
      if (err) return send(res, 404, 'Not found');
      const headers = Object.assign({}, CORS, { 'Content-Type': MIME[path.extname(file).toLowerCase()] || 'application/octet-stream', 'Cache-Control': 'no-cache' });
      res.writeHead(200, headers);
      res.end(data);
    });
    return true;
  }
  return false;
}

function handle(req, res, body){
  const url = (req.url || '/').split('?')[0];
  const m = req.method;

  if (m === 'OPTIONS') return send(res, 204);

  // POST do próprio jogo (o .cfg aponta pra http://localhost:3000/).
  // Corpo vazio/malformado é só ignorado - nunca derruba o servidor.
  if (m === 'POST' && url === '/'){
    try {
      const json = parseBody(body);
      if (obj(json)) latest = mapGameState(json);
    } catch (e) {
      console.log('[AVISO] Ignorando POST do GSI que não pôde ser processado: ' + e.message);
    }
    return send(res, 200, '');
  }

  if (m === 'GET' && url === '/') return send(res, 200, 'CS2 HUD Exporter rodando. Use GET /api/hud para pegar o estado atual.');

  if (m === 'GET' && url === '/api/hud'){
    if (!latest) return send(res, 204);
    latest.teams = teamsStore.resolve(latest.players, false);
    latest.series = seriesStore.series;
    latest.cams = camsStore.cams;
    return send(res, 200, latest);
  }

  // ---- times (control.html) ----
  if (url === '/api/teams'){
    if (m === 'GET') return send(res, 200, teamsStore.resolve(currentPlayers(), true));
    if (m === 'POST'){ teamsStore.set(obj(parseBody(body)) || {}, currentPlayers()); return send(res, 200, teamsStore.resolve(currentPlayers(), true)); }
    if (m === 'DELETE'){ teamsStore.clear(); return send(res, 200, teamsStore.resolve(currentPlayers(), true)); }
  }
  if (m === 'POST' && url === '/api/teams/swap'){
    teamsStore.swap(currentPlayers());
    return send(res, 200, teamsStore.resolve(currentPlayers(), true));
  }
  if (m === 'GET' && url.startsWith('/api/teams/logo/')){
    const logo = teamsStore.getLogo(decodeURIComponent(url.slice('/api/teams/logo/'.length)));
    return logo ? send(res, 200, logo.bytes, logo.type) : send(res, 404, 'Not found');
  }

  // ---- webcams (control.html) ----
  if (url === '/api/cams'){
    if (m === 'GET') return send(res, 200, camsStore.cams);
    if (m === 'POST'){ camsStore.set(obj(parseBody(body)) || {}); return send(res, 200, camsStore.cams); }
  }

  // ---- série de mapas (CS2MapVeto) ----
  if (url === '/api/series'){
    if (m === 'GET') return send(res, 200, seriesStore.series);
    if (m === 'POST'){ seriesStore.set(parseBody(body)); return send(res, 200, seriesStore.series); }
    if (m === 'DELETE'){ seriesStore.set(null); return send(res, 200, ''); }
  }

  if (m === 'GET' && serveStatic(url, res)) return;

  return send(res, 404, 'Not found');
}

function onRequest(req, res){
  const chunks = [];
  req.on('data', c => chunks.push(c));
  req.on('end', () => {
    try { handle(req, res, Buffer.concat(chunks).toString('utf8')); }
    catch (e) {
      console.log('[AVISO] Erro ao responder ' + req.method + ' ' + req.url + ': ' + e.message);
      try { send(res, 500, 'Erro'); } catch (e2) { /* conexão já fechada */ }
    }
  });
  req.on('error', () => { /* conexão caiu no meio: ignora */ });
}

// "localhost" pode ser 127.0.0.1 (IPv4) ou ::1 (IPv6) dependendo do
// Windows, então escutamos nos dois. Só a própria máquina acessa.
function listen(port){
  return new Promise((resolve, reject) => {
    const v4 = http.createServer(onRequest);
    v4.on('error', reject);
    v4.listen(port, '127.0.0.1', () => {
      const v6 = http.createServer(onRequest);
      v6.on('error', () => { /* sem IPv6: tudo bem, o IPv4 já está ouvindo */ });
      v6.listen(port, '::1');
      resolve();
    });
  });
}

// =========================================================================
// Início
// =========================================================================
teamsStore.load();
seriesStore.load();
camsStore.load();

try {
  if (!writeGsiConfig()){
    console.log('[AVISO] Não achei a pasta do CS2 pra gerar a configuração do GSI.');
    console.log('        Crie manualmente em: <CS2>/game/csgo/cfg/' + GSI_FILE);
    console.log('        (veja o modelo no README.md deste projeto).');
  }
} catch (e) {
  console.log('[AVISO] Não consegui gravar a configuração do GSI: ' + e.message);
}

Promise.all([listen(GSI_PORT), listen(API_PORT)]).then(() => {
  console.log('Ouvindo o CS2 (GSI) em http://localhost:' + GSI_PORT + '/ ...');
  console.log('API da HUD disponível em http://localhost:' + API_PORT + '/api/hud ...');
  console.log('HUD (use esta URL no OBS): http://localhost:' + API_PORT + '/hud/hud.html');
  console.log('Painel: http://localhost:' + API_PORT + '/hud/control.html  |  Veto: http://localhost:' + API_PORT + '/veto/' + vetoFileName());
}).catch(e => {
  console.log('[ERRO] Não consegui abrir as portas ' + GSI_PORT + '/' + API_PORT + ': ' + e.message);
  console.log('       Feche outro exportador que esteja aberto e tente de novo.');
  process.exit(1);
});
