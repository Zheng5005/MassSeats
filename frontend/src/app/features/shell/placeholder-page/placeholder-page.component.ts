import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-placeholder-page',
  imports: [],
  templateUrl: './placeholder-page.html',
})
export class PlaceholderPage {
  @Input() title = 'Page';
}
