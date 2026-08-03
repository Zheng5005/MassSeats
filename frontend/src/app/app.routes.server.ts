import { RenderMode, ServerRoute } from '@angular/ssr';

const paramRoutes: ServerRoute[] = [
  'events/:id',
  'venues/:id',
  'admin/events/:id',
  'admin/venues/:id',
  'events/:id/book',
  'reservations/:id',
].map((path) => ({
  path,
  renderMode: RenderMode.Prerender,
  getPrerenderParams: () => [],
}));

export const serverRoutes: ServerRoute[] = [
  ...paramRoutes,
  {
    path: '**',
    renderMode: RenderMode.Prerender,
  },
];
