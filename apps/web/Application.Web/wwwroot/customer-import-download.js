// Download only the explicitly requested, authorized CSV; release temporary browser bytes.
export async function downloadCsv(filename, streamReference) {
    const bytes = await streamReference.arrayBuffer();
    const url = URL.createObjectURL(new Blob([bytes], { type: 'text/csv;charset=utf-8' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    try { link.click(); }
    finally { link.remove(); URL.revokeObjectURL(url); }
}
