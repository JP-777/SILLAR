import { Pagination } from 'sillar-frontend';

export function Intermedia() {
  return <Pagination page={3} totalPages={8} totalItems={152} onChange={() => {}} />;
}

export function PrimeraPagina() {
  return <Pagination page={1} totalPages={8} totalItems={152} onChange={() => {}} />;
}

export function UnaPagina() {
  return <Pagination page={1} totalPages={1} totalItems={7} onChange={() => {}} />;
}
