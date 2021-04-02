try {
    if (Get-Command mmdc -ErrorAction Stop) {
        gci diagrams/*.mmd | % { $fileName=$_.Name; $baseName = $_.BaseName+".png"; mmdc -i diagrams/$fileName -o images/$baseName};
    }
    
}
catch {
    echo "Please install mermaid-cli using this link: https://github.com/mermaid-js/mermaid-cli#install-locally"
}
