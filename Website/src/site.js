const menuButton = document.querySelector('.menu-toggle');
const navigation = document.getElementById('primary-nav');
document.documentElement.classList.add('js');
function closeMenu(){menuButton?.setAttribute('aria-expanded','false');navigation?.classList.remove('is-open');document.body.classList.remove('menu-open');}
menuButton?.addEventListener('click',()=>{const open=menuButton.getAttribute('aria-expanded')!=='true';menuButton.setAttribute('aria-expanded',String(open));navigation.classList.toggle('is-open',open);document.body.classList.toggle('menu-open',open);});
navigation?.addEventListener('click',event=>{if(event.target.closest('a'))closeMenu();});
document.addEventListener('keydown',event=>{if(event.key==='Escape' && menuButton?.getAttribute('aria-expanded')==='true'){closeMenu();menuButton.focus();}});
const desktop = window.matchMedia('(min-width:761px)');
desktop.addEventListener('change',()=>{if(desktop.matches)closeMenu();});
const dialog=document.getElementById('image-viewer');
document.querySelectorAll('[data-enlarge]').forEach(button=>button.addEventListener('click',()=>{const source=button.querySelector('img');dialog.querySelector('img').src=source.currentSrc||source.src;dialog.querySelector('img').alt=source.alt;dialog.querySelector('p').textContent=source.alt;dialog.showModal();}));
dialog?.querySelector('.dialog-close').addEventListener('click',()=>dialog.close());
dialog?.addEventListener('click',event=>{const bounds=dialog.getBoundingClientRect();if(event.target===dialog&&(event.clientX<bounds.left||event.clientX>bounds.right||event.clientY<bounds.top||event.clientY>bounds.bottom))dialog.close();});
const contents = document.querySelector('.contents-toggle');
if (contents && window.matchMedia('(max-width:760px)').matches) contents.open = false;
