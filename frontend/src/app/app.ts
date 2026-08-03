import { Component } from '@angular/core';

import { AppShell } from './core/shell/app-shell.component';

@Component({
  selector: 'app-root',
  imports: [AppShell],
  template: `<app-shell />`,
})
export class App {}
