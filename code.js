// TaskDrive (اسم مؤقت) - Figma plugin: يبني الشاشات الأربع الأساسية بحالاتها الخاصة
// عربي أولاً (RTL) - موبايل 390px - الأرقام والنسب أمثلة مؤقتة وتتضبط بعد المقابلات

const C = {
  ink: '#111827', teal: '#0F766E', tealLight: '#CCFBF1', bg: '#F9FAFB', card: '#FFFFFF',
  line: '#E5E7EB', gray: '#4B5563', muted: '#9CA3AF',
  green: '#15803D', greenBg: '#DCFCE7', red: '#B91C1C', redBg: '#FEE2E2',
  amber: '#B45309', amberBg: '#FEF3C7', grayBg: '#F3F4F6'
};
const FONT = 'Cairo';

function rgb(h) { const n = parseInt(h.slice(1), 16); return { r: ((n >> 16) & 255) / 255, g: ((n >> 8) & 255) / 255, b: (n & 255) / 255 }; }
const solid = (h) => [{ type: 'SOLID', color: rgb(h) }];
const growSet = new Set();
const grow = (n) => { growSet.add(n); return n; };

// ---------- عناصر أساسية ----------
function txt(s, o = {}) {
  const t = figma.createText();
  t.fontName = { family: FONT, style: o.bold ? 'Bold' : 'Regular' };
  t.characters = s;
  t.fontSize = o.size || 14;
  t.fills = solid(o.color || C.ink);
  t.textAlignHorizontal = o.align || 'RIGHT';
  t.textAutoResize = 'WIDTH_AND_HEIGHT';
  return t;
}

function box(name, o, kids = []) {
  const f = figma.createFrame();
  f.name = name;
  f.layoutMode = o.dir || 'VERTICAL';
  f.itemSpacing = o.gap !== undefined ? o.gap : 8;
  const p = o.pad || 0;
  f.paddingLeft = f.paddingRight = o.px !== undefined ? o.px : p;
  f.paddingTop = f.paddingBottom = o.py !== undefined ? o.py : p;
  f.fills = o.fill ? solid(o.fill) : [];
  f.cornerRadius = o.r || 0;
  if (o.stroke) { f.strokes = solid(o.stroke); f.strokeWeight = 1; }
  f.primaryAxisSizingMode = 'AUTO';
  f.counterAxisSizingMode = 'AUTO';
  if (o.align) f.counterAxisAlignItems = o.align;
  if (o.main) f.primaryAxisAlignItems = o.main;
  if (o.minH) f.minHeight = o.minH;
  // RTL: أول عنصر في الصف يبقى على اليمين
  const list = o.dir === 'HORIZONTAL' ? [...kids].reverse() : kids;
  for (const k of list) {
    f.appendChild(k);
    if (f.layoutMode === 'VERTICAL') {
      k.layoutAlign = 'STRETCH';
      if (k.type === 'TEXT') k.textAutoResize = 'HEIGHT';
      else if (k.type === 'FRAME') {
        if (k.layoutMode === 'HORIZONTAL') k.primaryAxisSizingMode = 'FIXED';
        else if (k.layoutMode === 'VERTICAL') k.counterAxisSizingMode = 'FIXED';
      }
    } else if (growSet.has(k)) {
      k.layoutGrow = 1;
      if (k.type === 'TEXT') k.textAutoResize = 'HEIGHT';
      else if (k.type === 'FRAME' && k.layoutMode === 'VERTICAL') k.counterAxisSizingMode = 'AUTO';
    }
  }
  return f;
}

const row = (name, kids, o = {}) => box(name, Object.assign({ dir: 'HORIZONTAL', align: 'CENTER', gap: 8 }, o), kids);

function btn(label, kind = 'primary') {
  const s = {
    primary: { fill: C.teal, color: '#FFFFFF', stroke: null },
    secondary: { fill: '#FFFFFF', color: C.teal, stroke: C.teal },
    danger: { fill: '#FFFFFF', color: C.red, stroke: C.red },
    disabled: { fill: C.grayBg, color: C.muted, stroke: null }
  }[kind];
  const f = box('زر / ' + label, { dir: 'HORIZONTAL', main: 'CENTER', align: 'CENTER', py: 12, px: 16, r: 12, fill: s.fill, stroke: s.stroke }, [txt(label, { bold: true, color: s.color, size: 15 })]);
  return f;
}

function chip(label, tone = 'gray') {
  const t = {
    gray: [C.grayBg, C.gray], green: [C.greenBg, C.green], red: [C.redBg, C.red],
    amber: [C.amberBg, C.amber], teal: [C.tealLight, C.teal]
  }[tone];
  return box('شارة / ' + label, { dir: 'HORIZONTAL', px: 10, py: 4, r: 99, fill: t[0], align: 'CENTER' }, [txt(label, { size: 12, bold: true, color: t[1] })]);
}

function note(msg, tone = 'amber') {
  const t = { amber: [C.amberBg, C.amber], red: [C.redBg, C.red], green: [C.greenBg, C.green], teal: [C.tealLight, C.teal], gray: [C.grayBg, C.gray] }[tone];
  return box('ملحوظة', { dir: 'HORIZONTAL', px: 12, py: 10, r: 10, fill: t[0], align: 'CENTER' }, [grow(txt(msg, { size: 13, color: t[1] }))]);
}

function field(label, placeholder, o = {}) {
  const input = box('حقل', { dir: 'HORIZONTAL', px: 12, py: 12, r: 10, fill: '#FFFFFF', stroke: o.error ? C.red : C.line, minH: o.h || 0, align: o.h ? 'MIN' : 'CENTER' },
    [grow(txt(placeholder, { size: 14, color: o.value ? C.ink : C.muted }))]);
  if (o.h) input.minHeight = o.h;
  const kids = [txt(label, { size: 13, bold: true, color: C.gray }), input];
  if (o.error) kids.push(txt(o.error, { size: 12, color: C.red }));
  return box('حقل / ' + label, { gap: 6 }, kids);
}

function card(name, kids, o = {}) {
  return box(name, { pad: 14, gap: o.gap || 10, r: 14, fill: '#FFFFFF', stroke: o.stroke || C.line }, kids);
}

function header(title, back = true) {
  const kids = [grow(txt(title, { size: 20, bold: true }))];
  if (back) kids.push(txt('›', { size: 24, color: C.gray }));
  return row('هيدر', kids, { gap: 8 });
}

function phone(name, x, y, kids) {
  const f = box(name, { gap: 16, pad: 16, fill: C.bg }, kids);
  f.resize(390, 844);
  f.primaryAxisSizingMode = 'AUTO';
  f.counterAxisSizingMode = 'FIXED';
  f.minHeight = 844;
  f.x = x; f.y = y;
  figma.currentPage.appendChild(f);
  return f;
}

function sectionTitle(s, x, y) {
  const t = txt(s, { size: 28, bold: true, color: C.teal });
  t.x = x; t.y = y; figma.currentPage.appendChild(t);
}

function checkItem(label, o = {}) {
  const kids = [grow(txt(label, { size: 14 }))];
  if (o.status) kids.push(chip(o.status[0], o.status[1]));
  if (o.del) kids.push(txt('×', { size: 20, color: C.muted }));
  if (o.box) kids.unshift(box('checkbox', { dir: 'HORIZONTAL', main: 'CENTER', align: 'CENTER', fill: o.checked ? C.teal : '#FFFFFF', stroke: o.checked ? C.teal : C.muted, r: 5 }, [txt(o.checked ? '✓' : ' ', { size: 12, color: '#FFFFFF' })]));
  const item = row('بند', kids, { px: 12, py: 10, r: 10, fill: '#FFFFFF', stroke: o.warn ? C.amber : C.line, gap: 10 });
  if (!o.warn) return item;
  return box('بند + تنبيه', { gap: 4 }, [item, txt(o.warn, { size: 12, color: C.amber })]);
}

const big = (s, c) => txt(s, { size: 22, bold: true, color: c || C.ink });
const small = (s, c) => txt(s, { size: 12, color: c || C.gray });

// عنصرين الثقة: العداد وحالة الفلوس
function timerCard(state, reason) {
  const running = state === 'run';
  return card('عداد المدة', [
    row('عداد', [grow(small('المدة المتبقية')), chip(running ? 'العداد شغال' : 'العداد واقف', running ? 'green' : 'amber')]),
    big(running ? '٢ يوم ٤ ساعات' : '٢ يوم ٤ ساعات (متوقف)', running ? C.ink : C.amber),
    reason ? txt('سبب الوقف: ' + reason, { size: 13, color: C.amber }) : small('العداد بيقف تلقائي لو العميل اتأخر في الرد أو الملفات')
  ], { stroke: running ? C.line : C.amber });
}
function moneyCard(state) {
  const m = { held: ['محجوزة', 'teal', 'الفلوس محجوزة على المنصة لحد الموافقة'],
    paid: ['اتحولت للمستقل', 'green', 'تم تحويل المبلغ للمستقل'],
    back: ['مرجعة للعميل', 'red', 'تم إرجاع المبلغ للعميل'] }[state];
  return card('حالة الفلوس', [row('فلوس', [grow(small('حالة الفلوس')), chip(m[0], m[1])]), txt(m[2], { size: 14, bold: true })]);
}

// ---------- الشاشة 1: نموذج الطلب ----------
function screen1(x, y, variant) {
  const bad = variant === 'problem';
  const items = [
    checkItem('تصميم شعار بثلاث نسخ مختلفة', { del: true }),
    checkItem('تسليم الملفات بصيغة AI وPNG', { del: true })
  ];
  if (bad) items.push(checkItem('شكل حلو', { del: true, warn: 'البند ده غامض ومينفعش يتفحص. حدده أكتر (مثال: لونين أساسيين فقط).' }));
  const kids = [
    header(bad ? 'طلب جديد - حالات خاصة' : 'طلب جديد'),
    field('عنوان الطلب', 'مثال: تصميم شعار لمطعم'),
    field('المجال', 'اختار المجال ▾'),
    field('الوصف', 'اشرح اللي محتاجه بالتفصيل', { h: 90 }),
    row('ميزانية وموعد', [
      grow(field('الميزانية (جنيه)', bad ? '٢٠٠' : '٦٠٠', { value: bad, error: bad ? 'أقل من الحد الأدنى للمجال (مثال: ٣٠٠)' : null })),
      grow(field('الموعد المفضل', 'اختار تاريخ'))
    ], { align: 'MIN', gap: 10 }),
    box('قايمة المخرجات', { gap: 8 }, [
      txt('قايمة المخرجات', { size: 16, bold: true }),
      small('اكتب كل بند كجملة محددة ينفع تتفحص. كل ما كان البند أدق، الاتفاق أوضح للطرفين.'),
      box('أمثلة', { dir: 'HORIZONTAL', gap: 6 }, [chip('مثال: تصميم شعار بثلاث نسخ مختلفة', 'teal')])
    ].concat(items).concat([btn('+ إضافة بند', 'secondary')])),
  ];
  if (bad) kids.push(note('فيه بند غامض وميزانية أقل من الحد. صلّحهم قبل النشر.', 'red'));
  const pub = btn('نشر الطلب', bad ? 'disabled' : 'primary');
  kids.push(pub, btn('حفظ مسودة', 'secondary'));
  const f = phone(bad ? '1b - نموذج الطلب (بند غامض / ميزانية قليلة)' : '1a - نموذج الطلب (العميل)', x, y, kids);
  return { f, pub };
}

// ---------- الشاشة 2: العروض المضادة ----------
function offerCard(name, price, days, ontime) {
  const acc = btn('قبول العرض', 'primary');
  const counter = btn('عرض مضاد', 'secondary');
  const c = card('عرض / ' + name, [
    row('اسم', [grow(txt(name, { size: 16, bold: true })), chip('في الميعاد ' + ontime, 'green')]),
    row('تفاصيل', [chip(price, 'teal'), chip(days, 'gray')], { gap: 6 }),
    row('أزرار', [grow(acc), grow(counter)], { gap: 8 })
  ]);
  return { c, acc };
}

function screen2Client(x, y) {
  const o1 = offerCard('أحمد س.', '٧٠٠ جنيه', '٥ أيام', '٩٦٪');
  const o2 = offerCard('منى ك.', '٦٥٠ جنيه', '٦ أيام', '٩١٪');
  const o3 = offerCard('يوسف ع.', '٦٠٠ جنيه', '٧ أيام', '٨٨٪');
  const f = phone('2a - العروض المضادة (العميل)', x, y, [
    header('العروض على طلبك'),
    note('حدود التصنيف: السعر ٥٠٠ - ٨٠٠ جنيه، المدة ٣ - ٧ أيام (أمثلة)', 'teal'),
    o1.c, o2.c, o3.c
  ]);
  return { f, acc: o1.acc };
}

function screen2Freelancer(x, y, bad) {
  const kids = [
    header(bad ? 'تقديم عرض - برا الحدود' : 'تقديم عرض'),
    note('الحدود المسموحة: السعر ٥٠٠ - ٨٠٠ جنيه، المدة ٣ - ٧ أيام (أمثلة)', 'teal'),
    field('سعرك (جنيه)', bad ? '١٢٠٠' : '٧٠٠', { value: true, error: bad ? 'السعر أعلى من الحد المسموح (٨٠٠)' : null }),
    field('مدة التسليم اللي هتلتزم بيها', bad ? '٢ يوم' : '٥ أيام', { value: true, error: bad ? 'المدة أقل من الحد المسموح (٣ أيام)' : null }),
    note('لو اتأخرت عن المدة دي، هيتخصم عنك ١٠٪ عن كل يوم تأخير (مثال، النسبة هتتحدد). التأخير اللي سببه العميل مش بيتحسب عليك.', 'amber')
  ];
  if (bad) kids.push(note('العرض برا الحدود المسموحة للتصنيف. عدّل السعر والمدة عشان تقدر تبعته.', 'red'));
  kids.push(btn('إرسال العرض', bad ? 'disabled' : 'primary'));
  return phone(bad ? '2c - تقديم عرض برا الحدود (المستقل)' : '2b - تقديم عرض (المستقل)', x, y, kids);
}

// ---------- الشاشة 3: التنفيذ والتسليم ----------
function screen3(x, y, v) {
  // v: client | paused | freelancer
  const name = { client: '3a - التنفيذ (العميل)', paused: '3b - العداد واقف (العميل)', freelancer: '3c - التنفيذ (المستقل)' }[v];
  const timer = v === 'paused' ? timerCard('pause', 'بانتظار رد العميل') : timerCard('run');
  const list = box('قايمة المخرجات', { gap: 8 }, [
    txt('قايمة المخرجات وحالتها', { size: 16, bold: true }),
    checkItem('تصميم شعار بثلاث نسخ مختلفة', { status: ['مطابق', 'green'] }),
    checkItem('تسليم الملفات بصيغة AI وPNG', { status: ['لسه', 'gray'] }),
    checkItem('لونين أساسيين فقط', { status: ['غير مطابق', 'red'] })
  ]);
  const kids = [header('تنفيذ الطلب'), timer, moneyCard('held')];
  let approve = null, object = null;
  if (v === 'paused') kids.push(note('العداد واقف لحد ما ترد أو ترفع الملفات الأساسية. التأخير ده مش بيتحسب على المستقل.', 'amber'));
  kids.push(list);
  if (v === 'freelancer') {
    kids.push(note('الذكاء الاصطناعي هيراجع شغلك بند بند قبل ما يوصل للعميل.', 'teal'), btn('رفع الشغل', 'primary'));
  } else {
    const trial = box('نسخة تجريبية', { gap: 8 }, [
      txt('النسخة التجريبية', { size: 16, bold: true }),
      (() => { const r = figma.createRectangle(); r.resize(300, 120); r.cornerRadius = 12; r.fills = solid(C.grayBg); r.name = 'معاينة (علامة مائية)'; return r; })(),
      small('نسخة محدودة (علامة مائية / معاينة). النسخة الأصلية بتوصلك بعد الموافقة.')
    ]);
    trial.children.forEach(c => { if (c.type === 'RECTANGLE') { c.layoutAlign = 'STRETCH'; } });
    approve = btn('موافق', 'primary');
    object = btn('اعتراض', 'danger');
    kids.push(trial, row('قرار', [grow(approve), grow(object)], { gap: 10 }), small('متبقي لك ٢ محاولات اعتراض'));
  }
  const f = phone(name, x, y, kids);
  return { f, object };
}

// ---------- الشاشة 4: الاعتراض وتقرير المراجعة ----------
function report(withStatus) {
  return card('تقرير المقارنة', [
    txt('تقرير المقارنة بند بند', { size: 16, bold: true }),
    small('كتبه الذكاء الاصطناعي بمقارنة التنفيذ بالقايمة اللي الطرفين وافقوا عليها.'),
    checkItem('تصميم شعار بثلاث نسخ مختلفة', { status: ['مطابق', 'green'] }),
    checkItem('تسليم الملفات بصيغة AI وPNG', { status: ['مطابق', 'green'] }),
    checkItem('لونين أساسيين فقط', { status: ['غير مطابق', 'red'] })
  ]);
}

function screen4(x, y, v) {
  const names = {
    form: '4a - الاعتراض (العميل)', review: '4b - تحت المراجعة (العميل)', freelancer: '4c - الاعتراض (المستقل)',
    maxed: '4d - انتهت المحاولات', ok: '4e - النتيجة: مطابق', cut: '4f - النتيجة: خصم على المستقل'
  };
  const kids = [header(v === 'maxed' ? 'انتهت محاولات الاعتراض' : v === 'ok' || v === 'cut' ? 'نتيجة المراجعة' : 'اعتراض على التسليم')];
  if (v === 'form') {
    kids.push(
      txt('اختار البنود اللي معترض عليها', { size: 16, bold: true }),
      checkItem('تصميم شعار بثلاث نسخ مختلفة', { box: true }),
      checkItem('تسليم الملفات بصيغة AI وPNG', { box: true }),
      checkItem('لونين أساسيين فقط', { box: true, checked: true }),
      field('سبب الاعتراض', 'اكتب السبب بالتفصيل', { h: 80 }),
      report(),
      note('الذكاء الاصطناعي بيكتب التقرير بس، وما بيخصمش فلوس.', 'teal'),
      btn('إرسال الاعتراض', 'primary')
    );
  } else if (v === 'review' || v === 'freelancer') {
    kids.push(row('حالة', [grow(txt('حالة الاعتراض', { size: 16, bold: true })), chip('تحت مراجعة القسم المختص', 'amber')]),
      moneyCard('held'), report());
    if (v === 'freelancer') kids.push(card('اعتراض العميل', [txt('البند المعترض عليه: لونين أساسيين فقط', { size: 14, bold: true }), small('سبب العميل: الشعار فيه أكتر من لونين.')]), btn('اكتب ردك', 'primary'));
    else kids.push(note('هتوصلك النتيجة في الإشعارات بعد ما القسم المختص يخلص مراجعة التقرير.', 'gray'));
  } else if (v === 'maxed') {
    kids.push(note('المحاولات خلصت. الموضوع اتحول مباشرة للقسم المختص عشان يراجعه.', 'amber'),
      row('حالة', [grow(txt('حالة الاعتراض', { size: 16, bold: true })), chip('اتحول للقسم المختص', 'amber')]),
      moneyCard('held'), report());
  } else if (v === 'ok') {
    kids.push(note('الشغل مطابق للقايمة المتفق عليها. الفلوس اتحولت للمستقل.', 'green'), moneyCard('paid'), report());
  } else {
    kids.push(note('القسم المختص راجع التقرير وحدد خصم على المستقل.', 'red'),
      card('تفاصيل الخصم', [
        row('خصم', [grow(txt('قيمة الخصم', { size: 14, bold: true })), chip('٧٠ جنيه (مثال)', 'red')]),
        small('اتحسب إزاي: بند غير مطابق (لونين أساسيين فقط)، والخصم حدده القسم المختص بعد مراجعة التقرير.')
      ]), moneyCard('back'), report());
  }
  return phone(names[v], x, y, kids);
}

// ---------- التشغيل ----------
async function link(node, dest) {
  await node.setReactionsAsync([{
    trigger: { type: 'ON_CLICK' },
    actions: [{ type: 'NODE', destinationId: dest.id, navigation: 'NAVIGATE', transition: null, preserveScrollPosition: false }]
  }]);
}

(async () => {
  const styles = ['Regular', 'Bold'];
  try {
    for (const s of styles) await figma.loadFontAsync({ family: FONT, style: s });
  } catch (e) {
    figma.notify('خط Cairo مش متاح. فعّله من Google Fonts في Figma وجرب تاني.');
    figma.closePlugin();
    return;
  }

  const dx = 430, rowH = 1500;
  sectionTitle('١ - نموذج الطلب', 0, -70);
  const s1a = screen1(0, 0, 'ok');
  const s1b = screen1(dx, 0, 'problem');

  sectionTitle('٢ - العروض المضادة', 0, rowH - 70);
  const s2a = screen2Client(0, rowH);
  const s2b = screen2Freelancer(dx, rowH, false);
  const s2c = screen2Freelancer(dx * 2, rowH, true);

  sectionTitle('٣ - التنفيذ والتسليم والمراجعة', 0, rowH * 2 - 70);
  const s3a = screen3(0, rowH * 2, 'client');
  const s3b = screen3(dx, rowH * 2, 'paused');
  const s3c = screen3(dx * 2, rowH * 2, 'freelancer');

  sectionTitle('٤ - الاعتراض وتقرير المراجعة', 0, rowH * 3 - 70);
  const s4 = {};
  ['form', 'review', 'freelancer', 'maxed', 'ok', 'cut'].forEach((v, i) => { s4[v] = screen4(i * dx, rowH * 3, v); });

  // ربط النموذج (prototype)
  await link(s1a.pub, s2a.f);
  await link(s2a.acc, s3a.f);
  await link(s3a.object, s4.form);
  try { figma.currentPage.flowStartingPoints = [{ nodeId: s1a.f.id, name: 'TaskDrive - رحلة الطلب' }]; } catch (e) {}

  figma.viewport.scrollAndZoomIntoView(figma.currentPage.children);
  figma.notify('تم إنشاء الشاشات الأربع بحالاتها');
  figma.closePlugin();
})();
